"""Read-only audit of the captured Confluence CSVs against a Unity API export.

Run from the repository root. No asset or remote data is written.
"""
import csv
import io
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'Evidence' / 'DP14'
CATEGORIES = ['Unclassified', 'Medicine', 'Material', 'Equipment', 'Container', 'BusinessSign']


def records(text):
    rows = list(csv.reader(io.StringIO(text)))
    start = next(i for i, row in enumerate(rows) if row and row[0] == '_id')
    header = rows[start]
    result = []
    for row in rows[start + 1:]:
        if not row:
            continue
        if len(row) != len(header):
            raise ValueError('CSV column count mismatch')
        result.append(dict(zip(header, row)))
    return result


def index(rows, key):
    result = {}
    for row in rows:
        value = row[key]
        if not value or value in result:
            raise ValueError(f'Missing or duplicate ID: {value}')
        result[value] = row
    return result


def shape(coordinates, sketch):
    if not coordinates or not sketch:
        raise ValueError('Unspecified shape')
    points = []
    for pair in coordinates.split(';'):
        if not re.fullmatch(r'\s*\d+\s*,\s*\d+\s*', pair):
            raise ValueError('Invalid coordinate')
        points.append(tuple(map(int, pair.split(','))))
    if len(set(points)) != len(points):
        raise ValueError('Duplicate coordinate')
    rows = sketch.replace('\\n', '\n').splitlines()
    if not rows or any(not row or len(row) != len(rows[0]) or set(row) - {'#', '.'} for row in rows):
        raise ValueError('Invalid sketch')
    expected = {(x, y) for y, row in enumerate(rows) for x, value in enumerate(row) if value == '#'}
    if not expected or expected != set(points):
        raise ValueError('Sketch/coordinate mismatch')
    return sorted(points)


def validate_lines(lines, items, recipes):
    index(lines, '条目ID')
    seen = set()
    for row in lines:
        if row['物品ID'] not in items or row['所属配方ID'] not in recipes:
            raise ValueError('Unknown reference')
        if not re.fullmatch(r'[1-9]\d*', row['数量']):
            raise ValueError('Quantity must be a positive integer')
        if row['用途'] not in ('原料', '产物'):
            raise ValueError('Unknown recipe role')
        key = (row['所属配方ID'], row['用途'], row['物品ID'])
        if key in seen:
            raise ValueError('Duplicate recipe ingredient/output')
        seen.add(key)
    for recipe in recipes:
        if {r['用途'] for r in lines if r['所属配方ID'] == recipe} != {'原料', '产物'}:
            raise ValueError('Recipe needs input and output')


def audit():
    items = index(records((ROOT / 'items.csv').read_text(encoding='utf-8')), '物品ID')
    recipes = index(records((ROOT / 'recipes.csv').read_text(encoding='utf-8')), '配方ID')
    lines = records((ROOT / 'lines.csv').read_text(encoding='utf-8'))
    unity = json.loads((ROOT / 'unity-catalog.json').read_text(encoding='utf-8'))
    local = index(unity['items'], 'id')
    validate_lines(lines, items, recipes)
    report = {'counts': {'items': len(items), 'recipes': len(recipes), 'lines': len(lines)},
              'pending': [key for key, row in {**items, **recipes}.items() if row['数据状态'] == '待同步'],
              'baseline': [], 'shapeIssues': [], 'recipe': {}, 'applied': 0}
    for key, row in items.items():
        try:
            shape(row['格子坐标'], row['形状草图'])
        except ValueError as error:
            report['shapeIssues'].append({'id': key, 'state': row['数据状态'], 'reason': str(error)})
    for key, item in local.items():
        row = items.get(key)
        if row is None:
            report['baseline'].append({'id': key, 'missingRemote': True, 'action': 'retain'})
            continue
        remote = {'title': row['物品名称'], 'category': row['类别'], 'baseValue': int(row['基础价值']),
                  'description': row['描述'], 'cells': shape(row['格子坐标'], row['形状草图']),
                  'supplierAvailable': row['可供货'] == '是', 'procurementSign': row['业务招牌'] == '是'}
        values = dict(item, category=CATEGORIES[item['category']], cells=sorted((p['x'], p['y']) for p in item['cells']))
        differences = {field: {'unity': values[field], 'confluence': value}
                       for field, value in remote.items() if values[field] != value}
        color = list(map(float, row['显示颜色'].split(',')))
        if any(abs(item['color'][name] - value) > 1e-6 for name, value in zip('rgba', color)):
            differences['color'] = {'unity': item['color'], 'confluence': color}
        if row['吸引卖家类别'] and row['吸引卖家类别'] != CATEGORIES[item['advertisedCategory']]:
            differences['advertisedCategory'] = row['吸引卖家类别']
        report['baseline'].append({'id': key, 'state': row['数据状态'], 'differences': differences,
                                   'reservedFields': {k: row[k] for k in ['物品标签', '资源路径', '属性', '材料形态', '储存宽度', '储存高度']}})
    recipe = recipes['recipe_pill_basic']
    baseline_lines = [r for r in lines if r['所属配方ID'] == 'recipe_pill_basic']
    expected = {('原料', unity['herbId'], '1'), ('原料', unity['dewId'], '1'), ('产物', unity['productId'], '1')}
    report['recipe'] = {'id': recipe['配方ID'], 'state': recipe['数据状态'], 'integration': recipe['接入状态'],
                        'duration': recipe['制作耗时天'], 'device': recipe['制作设备ID'],
                        'unityStableRecipeId': None, 'lines': baseline_lines,
                        'fixedRecipeMatches': expected == {(r['用途'], r['物品ID'], r['数量']) for r in baseline_lines}}
    report['stones'] = {key: items[key]['数据状态'] for key in ('stone_low', 'stone_mid', 'stone_high')}
    report['legacyStoneIds'] = [key for key in items if key.startswith('stone_') and key not in report['stones']]
    return report


if __name__ == '__main__':
    print(json.dumps(audit(), ensure_ascii=False, indent=2))

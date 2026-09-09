import unittest
from audit_confluence_data import audit, index, records, shape, validate_lines


class DataAuditTests(unittest.TestCase):
    def test_csv_keeps_nulls_and_stable_ids(self):
        rows = records('field_name,type\nID,text\n\n_id,ID,optional\n1,stone_low,\n')
        self.assertEqual(rows, [{'_id': '1', 'ID': 'stone_low', 'optional': ''}])

    def test_duplicate_ids_rejected(self):
        with self.assertRaises(ValueError):
            index([{'id': 'a'}, {'id': 'a'}], 'id')

    def test_literal_newlines_restore_shape(self):
        self.assertEqual(shape('0,0;0,1;1,1', r'#.\n##'), [(0, 0), (0, 1), (1, 1)])

    def test_invalid_or_conflicting_shapes_rejected(self):
        for coords, drawing in [('0,0;0,0', '#'), ('-1,0', '#'), ('0,0', '##'), ('', '')]:
            with self.subTest(coords=coords), self.assertRaises(ValueError):
                shape(coords, drawing)

    def test_unknown_reference_and_invalid_quantity_rejected(self):
        valid = {'条目ID': 'r:input:a', '所属配方ID': 'r', '用途': '原料', '物品ID': 'a', '数量': '1'}
        output = dict(valid, 条目ID='r:output:a', 用途='产物')
        validate_lines([valid, output], {'a': {}}, {'r': {}})
        for change in [{'物品ID': 'missing'}, {'所属配方ID': 'unknown'}, {'数量': '0'}, {'数量': '1.5'}]:
            with self.subTest(change=change), self.assertRaises(ValueError):
                validate_lines([dict(valid, **change), output], {'a': {}}, {'r': {}})

    def test_current_snapshot_retains_drafts_and_does_not_apply(self):
        report = audit()
        self.assertEqual(report['counts'], {'items': 60, 'recipes': 7, 'lines': 35})
        self.assertEqual(report['pending'], [])
        self.assertEqual(report['applied'], 0)
        self.assertTrue(report['recipe']['fixedRecipeMatches'])
        self.assertEqual(report['recipe']['duration'], '')
        self.assertEqual(report['recipe']['device'], '')
        self.assertTrue(all(state == '草稿' for state in report['stones'].values()))
        self.assertEqual(report['legacyStoneIds'], [])


if __name__ == '__main__':
    unittest.main(verbosity=2)

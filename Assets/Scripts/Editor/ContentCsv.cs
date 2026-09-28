using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace XiuXianShop.Editor
{
    public sealed class ContentIssue
    {
        public string table, id, field, reason;
        public int line;
        public bool error;
        public override string ToString() => $"{(error?"错误":"提示")} · {table} · 行 {line} · {id} · {field}：{reason}";
    }
    public sealed class ContentRow
    {
        public string table, id;
        public int line;
        public Dictionary<string,string> values;
        public List<ContentIssue> issues;
        public string Get(string field) => values.TryGetValue(field,out var value)?value:"";
        public string Optional(string field,string previous) => Get(field)==""?previous??"":Get(field);
        public void Error(string field,string reason) => issues.Add(new ContentIssue{table=table,id=id,line=line,field=field,reason=reason,error=true});
        public string Required(string field)
        {
            string value=Get(field);
            if(string.IsNullOrWhiteSpace(value))Error(field,"必填字段为空。");
            return value;
        }
        public ContentOptionalInt Number(string field,int minimum,bool required=false,ContentOptionalInt previous=default)
        {
            string value=Get(field);
            if(value==""){if(required)Error(field,"必填整数为空。");return required?default:previous;}
            if(!int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int number) || number<minimum)
                Error(field,$"须为不小于 {minimum} 的整数。");
            return new ContentOptionalInt{hasValue=true,value=number};
        }
        public string OneOf(string field,params string[] allowed)
        {
            string value=Required(field);
            if(!allowed.Contains(value))Error(field,"允许值："+string.Join(" / ",allowed));
            return value;
        }
    }
    public static class ContentCsv
    {
        // Supports ordinary CSV and Confluence's schema/views/records envelope. Quoted newlines stay in a cell.
        public static List<ContentRow> Read(string text,string table,string idField,string[] requiredColumns,List<ContentIssue> issues)
        {
            var parsed=new List<(int line,List<string> cells)>();
            var cells=new List<string>();var cell=new StringBuilder();bool quoted=false,closed=false;
            int line=1,startLine=1;
            void Fail(string reason)=>issues.Add(new ContentIssue{table=table,line=line,field="CSV",reason=reason,error=true});
            for(int i=0;i<text.Length;i++)
            {
                char c=text[i];if(i==0 && c=='\uFEFF')continue;
                if(quoted)
                {
                    if(c=='"'){if(i+1<text.Length && text[i+1]=='"'){cell.Append('"');i++;}else{quoted=false;closed=true;}}
                    else{cell.Append(c);if(c=='\n')line++;}
                    continue;
                }
                if(c=='"')
                {
                    if(cell.Length!=0 || closed){Fail("引号必须从字段开头开始。");return new List<ContentRow>();}
                    quoted=true;continue;
                }
                if(c==',' || c=='\r' || c=='\n')
                {
                    cells.Add(cell.ToString());cell.Clear();closed=false;
                    if(c==',')continue;
                    if(c=='\r' && i+1<text.Length && text[i+1]=='\n')i++;
                    parsed.Add((startLine,cells));cells=new List<string>();line++;startLine=line;
                }
                else {if(closed){Fail("结束引号之后只能是分隔符。");return new List<ContentRow>();}cell.Append(c);}
            }
            if(quoted){Fail("引号未闭合。");return new List<ContentRow>();}
            if(cell.Length>0 || cells.Count>0 || closed){cells.Add(cell.ToString());parsed.Add((startLine,cells));}
            int headerIndex=parsed.FindIndex(r=>r.cells.Count>0 && r.cells[0]=="_id");
            bool envelope=parsed.Count>0 && parsed[0].cells[0]=="field_name";
            if(headerIndex<0 && !envelope)headerIndex=parsed.FindIndex(r=>r.cells.Contains(idField));
            string[] headers=headerIndex>=0?parsed[headerIndex].cells.ToArray():parsed.Skip(1).TakeWhile(r=>r.cells.Count>1).Select(r=>r.cells[0]).ToArray();
            if(headers.Distinct().Count()!=headers.Length){Fail("列名重复。");return new List<ContentRow>();}
            foreach(string name in requiredColumns)
                if(!headers.Contains(name))issues.Add(new ContentIssue{table=table,line=headerIndex>=0?parsed[headerIndex].line:1,field=name,reason="快照缺少列。",error=true});
            var rows=new List<ContentRow>();
            if(headerIndex<0)
            {
                if(!envelope)Fail("找不到数据表头。");
                return rows; // A Confluence database with schema but no _id block is explicitly empty.
            }
            foreach(var record in parsed.Skip(headerIndex+1))
            {
                if(record.cells.All(string.IsNullOrEmpty))continue;
                var values=record.cells;
                // Some Confluence exports append empty cells; never drop non-empty overflow data.
                if(values.Count>headers.Length && values.Skip(headers.Length).All(string.IsNullOrEmpty))values=values.Take(headers.Length).ToList();
                if(values.Count!=headers.Length)
                {
                    issues.Add(new ContentIssue{table=table,line=record.line,field="CSV",reason=$"列数 {values.Count}，应为 {headers.Length}。",error=true});continue;
                }
                var row=new ContentRow{table=table,line=record.line,issues=issues,values=headers.Select((h,i)=>(h,i)).ToDictionary(x=>x.h,x=>values[x.i])};
                row.id=row.Get(idField);rows.Add(row);
            }
            return rows;
        }
    }
}

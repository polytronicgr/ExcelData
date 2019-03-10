﻿using System.Collections.Generic;
using NPOI.SS.UserModel;

namespace TK.ExcelData
{
    public class ReadHelper
    {
        #region header
        public static List<string> GetHeader(ISheet sheet, int headerOffset = 0,int colStart=0,int colEnd=-1)
        {
            List<string> header = new List<string>();

            //first row is header as default
            IRow headerRow = sheet.GetRow(sheet.FirstRowNum + headerOffset);
            int l = colEnd < 1 ? headerRow.LastCellNum : (colEnd < headerRow.LastCellNum ? colEnd : headerRow.LastCellNum);
            for(int i=headerRow.FirstCellNum+colStart;i<l;++i)
            {
                header.Add(headerRow.GetCell(i).StringCellValue);
            }
            return header;
        }

        public static List<Field> PrepareHeaderFields(List<string> header, Schema schema)
        {
            List<Field> headerFields = new List<Field>();
            foreach (string name in header)
            {
                headerFields.Add(schema.GetField(name));
            }

            return headerFields;
        }
        #endregion

        #region cell
        public static object GetCellValue(ICell cell, TypeInfo dataType)
        {
            switch (dataType.sign)
            {
                case TypeInfo.Sign.Int:
                    return GetIntValue(cell);
                case TypeInfo.Sign.Float:
                    return GetFloatValue(cell);
                case TypeInfo.Sign.Long:
                    return GetLongValue(cell);
                case TypeInfo.Sign.Double:
                    return GetDoubleValue(cell);
                case TypeInfo.Sign.Boolean:
                    return GetBoolValue(cell);
                case TypeInfo.Sign.String:
                    return GetStringValue(cell);
                case TypeInfo.Sign.Array:
                case TypeInfo.Sign.List:
                case TypeInfo.Sign.Dictionary:
                    return GetCompositeValue(cell, dataType);
                case TypeInfo.Sign.Generic:
                    return GetGenericValue(cell, dataType);
                case TypeInfo.Sign.Object:
                    return GetObjectValue(cell, dataType);
                default:
                    break;
            }
            return null;
        }
        
        public static int GetIntValue(ICell cell)
        {
            switch (cell.CellType)
            {
                case CellType.Numeric:
                    return (int)cell.NumericCellValue;
                case CellType.String:
                    return int.Parse(cell.StringCellValue);
                case CellType.Boolean:
                    return cell.BooleanCellValue ? 1 : 0;
                default:
                    throw new System.Exception("can't convert to int from " + cell.CellType);
            }
        }

        public static long GetLongValue(ICell cell)
        {
            switch (cell.CellType)
            {
                case CellType.Numeric:
                    return (long)cell.NumericCellValue;
                case CellType.String:
                    return long.Parse(cell.StringCellValue);
                default:
                    throw new System.Exception("can't convert to long from " + cell.CellType);
            }
        }

        public static float GetFloatValue(ICell cell)
        {
            switch (cell.CellType)
            {
                case CellType.Numeric:
                    return (float)cell.NumericCellValue;
                case CellType.String:
                    return float.Parse(cell.StringCellValue);
                default:
                    throw new System.Exception("can't convert to float from " + cell.CellType);
            }
        }

        public static double GetDoubleValue(ICell cell)
        {
            switch (cell.CellType)
            {
                case CellType.Numeric:
                    return cell.NumericCellValue;
                case CellType.String:
                    return double.Parse(cell.StringCellValue);
                default:
                    throw new System.Exception("can't convert to double from " + cell.CellType);
            }
        }

        public static bool GetBoolValue(ICell cell)
        {
            switch (cell.CellType)
            {
                case CellType.Numeric:
                    return cell.NumericCellValue != 0;
                case CellType.String:
                    return bool.Parse(cell.StringCellValue);
                case CellType.Boolean:
                    return cell.BooleanCellValue;
                default:
                    throw new System.Exception("can't convert to bool from " + cell.CellType);
            }
        }

        public static string GetStringValue(ICell cell)
        {
            switch (cell.CellType)
            {
                case CellType.Numeric:
                    return cell.NumericCellValue.ToString();
                case CellType.Boolean:
                    return cell.BooleanCellValue.ToString();
                default:
                    return cell.StringCellValue;
            }
        }

        public static object GetCompositeValue(ICell cell, TypeInfo type)
        {
            if (IsLinkCell(cell))
            {
                switch (type.sign)
                {
                    case TypeInfo.Sign.Array:
                        return ReadLinkHelper.ReadLinkArray(cell, TypeInfo.Object);
                    case TypeInfo.Sign.List:
                        return ReadLinkHelper.ReadLinkList(cell, TypeInfo.Object);
                    case TypeInfo.Sign.Dictionary:
                        return ReadLinkHelper.ReadLinkDict(cell, null);
                    default:
                        return null;
                }
            }
            else
            {
                //as json data
                return Newtonsoft.Json.JsonConvert.DeserializeObject(cell.StringCellValue);
            }
        }

        public static object GetGenericValue(ICell cell, TypeInfo type)
        {
            if (IsLinkCell(cell))
            {
                switch (type.genericType.sign)
                {
                    case TypeInfo.Sign.Array:
                        return ReadLinkHelper.ReadLinkArray(cell, type.genericArguments[0]);
                    case TypeInfo.Sign.List:
                        return ReadLinkHelper.ReadLinkList(cell, type.genericArguments[0]);
                    case TypeInfo.Sign.Dictionary:
                        return ReadLinkHelper.ReadLinkDict(cell, null);
                    default:
                        return null;
                }
            }
            else
            {
                //as json data
                return Newtonsoft.Json.JsonConvert.DeserializeObject(cell.StringCellValue);
            }
        }

        public static object GetObjectValue(ICell cell, TypeInfo type)
        {
            if (IsLinkCell(cell))
            {
                return ReadLinkHelper.ReadLinkObject(cell,type);
            }
            else
            {
                //as json data
                return Newtonsoft.Json.JsonConvert.DeserializeObject(cell.StringCellValue);
            }
        }

        /// <summary>
        /// 格式：__XXX;xxx!A1F12;xxx!1F1;xxx!A1,2;xxx!,D2
        /// </summary>
        /// <param name="cell"></param>
        /// <returns></returns>
        public static bool IsLinkCell(ICell cell)
        {
            return cell.StringCellValue.StartsWith("__") || cell.StringCellValue.IndexOf("!")>-1;
        }
        #endregion

        #region row

        static Dictionary<string, object> ReadRowData(IRow row, List<Field> headerFields)
        {
            if (headerFields == null || headerFields.Count == 0) return null;

            Dictionary<string, object> data = new Dictionary<string, object>();
            IEnumerator<ICell> iter = row.GetEnumerator();
            int index = 0;

            Field field;

            while (iter.MoveNext() && index < headerFields.Count)
            {
                field = headerFields[index];
                data[field.name] = GetCellValue(iter.Current, field.type);
                ++index;
            }

            return data;
        }

        static Dictionary<string, object> ReadRowData(IRow row, List<Field> headerFields, int colStart,int colEnd=-1)
        {
            if (headerFields == null || headerFields.Count == 0) return null;

            Dictionary<string, object> data = new Dictionary<string, object>();
            int index = 0;

            Field field;
            int l = colEnd < 1 ? row.LastCellNum : (colEnd < row.LastCellNum ? colEnd : row.LastCellNum);
            //offset 相对于0开始，excel最左边一列不能为空
            for (int i = row.FirstCellNum + colStart; i < l; ++i)
            {
                field = headerFields[index];
                data[field.name] = GetCellValue(row.GetCell(i), field.type);
                ++index;
            }

            return data;
        }
        #endregion

        #region list
        public static List<object> ReadList(ISheet sheet, Schema schema)
        {
            return ReadList(sheet, schema, Constance.SchemaDataRow, -1,0, -1,null);
        }

        public static List<object> ReadList(ISheet sheet, Schema schema, int dataStart)
        {
            return ReadList(sheet, schema, dataStart, -1,0,-1, null);
        }

        public static List<object> ReadList(ISheet sheet, Schema schema, int dataStart,int dataEnd)
        {
            return ReadList(sheet, schema, dataStart, dataEnd,0, -1,null);
        }

        public static List<object> ReadList(ISheet sheet, Schema schema, int dataStart, int dataEnd, int colStart, int colEnd, List<string> header)
        {

            if (header == null || header.Count == 0)
            {
                header = ReadHelper.GetHeader(sheet, 0, colStart,colEnd);
            }

            List<Field> headerFields = ReadHelper.PrepareHeaderFields(header, schema);

            List<object> list = new List<object>();
            int l = dataEnd <= 0 ? sheet.LastRowNum :(dataEnd < sheet.LastRowNum ? dataEnd : sheet.LastRowNum);
            for (int i = sheet.FirstRowNum + dataStart; i <= l; ++i)
            {
                Dictionary<string, object> record = ReadRowData(sheet.GetRow(i), headerFields, colStart,colEnd);
                list.Add(record);
            }
            return list;
        }

        #endregion

        #region dictionary

        public static Dictionary<string, object> ReadDictionary(ISheet sheet, Schema schema)
        {
            return ReadDictionary(sheet, schema, "", Constance.SchemaDataRow, 0, -1,null);
        }

        public static Dictionary<string, object> ReadDictionary(ISheet sheet, Schema schema, string keyField)
        {
            return ReadDictionary(sheet, schema, keyField, Constance.SchemaDataRow, 0, -1,null);
        }

        public static Dictionary<string, object> ReadDictionary(ISheet sheet, Schema schema, string keyField, int dataStart, int colStart, int colEnd, List<string> header, bool removeKeyInElement = false, int dataEnd=-1)
        {

            if (header == null || header.Count == 0)
            {
                header = ReadHelper.GetHeader(sheet, 0, colStart,colEnd);
            }

            List<Field> headerFields = ReadHelper.PrepareHeaderFields(header, schema);

            //如果没指定key,则默认使用第一个
            if (string.IsNullOrEmpty(keyField))
            {
                keyField = header[0];
            }

            Dictionary<string, object> dict = new Dictionary<string, object>();
            int l = dataEnd <= 0 ? sheet.LastRowNum : (dataEnd < sheet.LastRowNum ? dataEnd : sheet.LastRowNum);
            for (int i = sheet.FirstRowNum + dataStart; i <= l; ++i)
            {
                Dictionary<string, object> record = ReadRowData(sheet.GetRow(i), headerFields, colStart,colEnd);
                string key = record[keyField].ToString();
                dict[key] = record;
                if (removeKeyInElement)
                {
                    record.Remove(keyField);
                }
            }
            return dict;
        }
        #endregion
    }
}
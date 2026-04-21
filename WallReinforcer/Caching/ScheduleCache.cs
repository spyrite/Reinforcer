using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Assistants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Caching
{
    public class ScheduleCache
    {
        private Document doc;
        private Dictionary<ScheduleFieldId, Dictionary<string, string>> calculatedValuesDicts;

        public ViewSchedule Schedule { get; }
        public string Name { get; }
        public bool WithTitle { get; }
        public FieldLists Fields { get; set; }
        public List<FilterData> FiltersData { get; set; }
        public List<SortGroupFieldData> SGFsData { get; set; }
        public ScheduleSheetInstanceData SSIData { get; set; }

        public struct FieldLists
        {
            public List<ScheduleField> All;
            public List<ScheduleField> Qunatity;
            public List<ScheduleField> Visible;
        }
        public struct FilterData
        {
            public ScheduleFilter Filter;
            public ScheduleField Field;
            public ScheduleFilterType FilterType;
            public ElementId ParId;
            public object Value;
            public FilterRule Rule;
        }
        public struct SortGroupFieldData
        {
            public ScheduleSortGroupField SGF;
            public ScheduleField Field;
            public string Name;
            public bool IsCalculated;
            public Dictionary<string, string> CalculatedValues;
            public ScheduleSortOrder Order;

#if REVIT2023 || REVIT2024 || REVIT2025
            public ForgeTypeId DataType;
#else
            public ParameterType DataType;
#endif
        }
        public struct ScheduleSheetInstanceData
        {
            public ScheduleSheetInstance SSI;
            public double W;
            public double H;
            public Outline BoxOutline;
        }

        public ScheduleCache(ViewSchedule schedule)
        {
            doc = schedule.Document;
            Schedule = schedule;
            Name = Schedule.Name;
            WithTitle = Schedule.Definition.ShowTitle;
            //UpdateData(false);
        }

        public void SetSSIDdata(ScheduleSheetInstance ssi)
        {
            ViewSheet sheet = doc.GetElement(ssi.OwnerViewId) as ViewSheet;
            XYZ minP = ssi.get_BoundingBox(sheet).Min;
            XYZ maxP = ssi.get_BoundingBox(sheet).Max;
            double h;
            if (WithTitle) h = Math.Abs(maxP.Y - minP.Y) - 2.1 / 304.8;
            else h = Math.Abs(maxP.Y - minP.Y) - 4.2 / 304.8;
            SSIData = new ScheduleSheetInstanceData
            {
                SSI = ssi,
                W = Math.Abs(maxP.X - minP.X) - 4.2 / 304.8,
                H = h,
                BoxOutline = new Outline(minP, maxP)
            };
        }
        public void UpdateData(bool includeCalculatedValues)
        {
            SGFsData = GetSGFsData(includeCalculatedValues);
            Fields = GetFieldLists();
            FiltersData = GetFiltersData();
        }

        private List<FilterData> GetFiltersData()
        {
            List<FilterData> filteresData = new List<FilterData>();
            foreach (ScheduleFilter filter in Schedule.Definition.GetFilters().ToList())
            {
                FilterData filterData = new FilterData
                {
                    Filter = filter,
                    Field = Schedule.Definition.GetField(filter.FieldId),
                };
                if (filterData.Field.HasSchedulableField)
                {
                    filterData.FilterType = filter.FilterType;
                    filterData.ParId = filterData.Field.GetSchedulableField().ParameterId;
                    filterData.Value = GetFilterValue(filter);
                    filterData.Rule = GetFilterRule(filterData);
                }
                ;
                filteresData.Add(filterData);
            }
            return filteresData;
        }
        private List<SortGroupFieldData> GetSGFsData(bool includeCalculatedValues)
        {
            if (includeCalculatedValues) GetCalculatedValuesDicts();
            List<SortGroupFieldData> sgfsData = new List<SortGroupFieldData>();
            foreach (ScheduleSortGroupField sgf in Schedule.Definition.GetSortGroupFields().ToList())
            {
                SortGroupFieldData sgfData = new SortGroupFieldData
                {
                    SGF = sgf,
                    Field = Schedule.Definition.GetField(sgf.FieldId),
                    Order = sgf.SortOrder
                };
                sgfData.Name = sgfData.Field.GetName();
                sgfData.IsCalculated = sgfData.Field.IsCalculatedField;
                if (includeCalculatedValues && sgfData.IsCalculated)
                    sgfData.CalculatedValues = calculatedValuesDicts[sgf.FieldId];

                if (sgfData.Field.ParameterId != ElementId.InvalidElementId)
                {
#if REVIT2023
                    if (sgfData.Field.ParameterId.IntegerValue < 0)
                    {
                        Element elem = new FilteredElementCollector(doc, Schedule.Id).WhereElementIsNotElementType().First();
                        Parameter par = ElementParametersAssistant.GetParameterFromElement(elem, sgfData.Field.ParameterId);
                        sgfData.DataType = par.Definition.GetDataType();
                    }
                    else
                    {
                        ParameterElement parElem = doc.GetElement(sgfData.Field.ParameterId) as ParameterElement;
                        InternalDefinition def = parElem.GetDefinition();
                        sgfData.DataType = def.GetDataType();
                    }

#elif REVIT2024 || REVIT2025
                    if (sgfData.Field.ParameterId.Value < 0)
                    {
                        Element elem = new FilteredElementCollector(doc, Schedule.Id).WhereElementIsNotElementType().First();
                        Parameter par = ElementParametersAssistant.GetParameterFromElement(elem, sgfData.Field.ParameterId);
                        sgfData.DataType = par.Definition.GetDataType();
                    }
                    else
                    {
                        ParameterElement parElem = doc.GetElement(sgfData.Field.ParameterId) as ParameterElement;
                        InternalDefinition def = parElem.GetDefinition();
                        sgfData.DataType = def.GetDataType();
                    }

#else
                    if (sgfData.Field.ParameterId.IntegerValue < 0)
                    {
                        Element elem = new FilteredElementCollector(doc, Schedule.Id).WhereElementIsNotElementType().First();
                        Parameter par = elem.get_Parameter((BuiltInParameter)sgfData.Field.ParameterId.IntegerValue);
                        sgfData.DataType = par.Definition.ParameterType;
                    }
                    else
                    {
                        ParameterElement parElem = doc.GetElement(sgfData.Field.ParameterId) as ParameterElement;
                        InternalDefinition def = parElem.GetDefinition();
                        sgfData.DataType = def.ParameterType;
                    }
#endif
                }
                sgfsData.Add(sgfData);
            }
            return sgfsData;
        }
        private void GetCalculatedValuesDicts()
        {
            List<ScheduleSortGroupField> calculatedSGFs = (from sgf in Schedule.Definition.GetSortGroupFields()
                                                           where Schedule.Definition.GetField(sgf.FieldId).IsCalculatedField
                                                           select sgf).ToList();
            if (calculatedSGFs.Count > 0)
            {
                calculatedValuesDicts = new Dictionary<ScheduleFieldId, Dictionary<string, string>>();
                TableSectionData tsd = Schedule.GetTableData().GetSectionData(SectionType.Body);
#if REVIT2024 || REVIT2025
                SchedulableField schedulableIFCGuidField = (from field in Schedule.Definition.GetSchedulableFields()
                                                            where field.ParameterId.Value == (long)BuiltInParameter.IFC_GUID
                                                            select field).FirstOrDefault();
#else
                SchedulableField schedulableIFCGuidField = (from field in Schedule.Definition.GetSchedulableFields()
                                                            where field.ParameterId.IntegerValue == (int)BuiltInParameter.IFC_GUID
                                                            select field).FirstOrDefault();
#endif

                using (SubTransaction subTx = new SubTransaction(doc))
                {
                    subTx.Start();

                    //Подготовка
                    ScheduleField tempScheduleField = Schedule.Definition.AddField(schedulableIFCGuidField);
                    tempScheduleField.IsHidden = false;
                    Schedule.Definition.ShowTitle = false;
                    Schedule.Definition.ShowHeaders = false;
                    Schedule.Definition.IsItemized = true;
                    foreach (ScheduleSortGroupField sgf in calculatedSGFs)
                    {
                        ScheduleField field = Schedule.Definition.GetField(sgf.FieldId);
                        field.IsHidden = false;
                    }
                    Schedule.RefreshData();
                    int nCol2 = tsd.LastColumnNumber;
                    List<ScheduleField> visibleFields = (from id in Schedule.Definition.GetFieldOrder()
                                                         where Schedule.Definition.GetField(id).IsHidden == false
                                                         select Schedule.Definition.GetField(id)).ToList();

                    //Считывание данных
                    foreach (ScheduleSortGroupField sgf in calculatedSGFs)
                    {
                        Dictionary<string, string> calculatedValues = new Dictionary<string, string>();
                        ScheduleField field = Schedule.Definition.GetField(sgf.FieldId);
                        int nCol1 = (from i in Enumerable.Range(0, visibleFields.Count)
                                     where visibleFields[i].FieldId.IntegerValue == field.FieldId.IntegerValue
                                     select i).First();
                        for (int i = 0; i < tsd.NumberOfRows; i++)
                            calculatedValues.Add(Schedule.GetCellText(SectionType.Body, i, nCol2), Schedule.GetCellText(SectionType.Body, i, nCol1));
                        calculatedValuesDicts.Add(sgf.FieldId, calculatedValues);
                    }

                    subTx.RollBack();
                }
            }
        }
        private FieldLists GetFieldLists()
        {
            FieldLists fieldLists = new FieldLists
            {
                All = (from id in Schedule.Definition.GetFieldOrder()
                       select Schedule.Definition.GetField(id)).ToList(),
                Qunatity = (from id in Schedule.Definition.GetFieldOrder()
                            where Schedule.Definition.GetField(id).GetName().StartsWith("Количество ")
                            select Schedule.Definition.GetField(id)).ToList(),
                Visible = (from id in Schedule.Definition.GetFieldOrder()
                           where Schedule.Definition.GetField(id).IsHidden == false
                           select Schedule.Definition.GetField(id)).ToList()
            };

            return fieldLists;
        }
        private object GetFilterValue(ScheduleFilter filter)
        {
            if (filter.IsDoubleValue) return filter.GetDoubleValue();
            else if (filter.IsElementIdValue) return filter.GetElementIdValue();
            else if (filter.IsIntegerValue) return filter.GetIntegerValue();
            else if (filter.IsStringValue) return filter.GetStringValue();
            else return null;
        }
        private FilterRule GetFilterRule(FilterData filterData)
        {
            FilterRule filterRule = null;
            //if (filterData.Value != null && double.TryParse(filterData.Value.ToString(), out double parsedNumber))
#if REVIT2024 || REVIT2025
            if (filterData.Filter.IsDoubleValue || filterData.Filter.IsIntegerValue
                || filterData.ParId.Value == (int)BuiltInParameter.PHASE_CREATED
                || filterData.ParId.Value == (int)BuiltInParameter.SCHEDULE_LEVEL_PARAM)
#else
            if (filterData.Filter.IsDoubleValue || filterData.Filter.IsIntegerValue
                || filterData.ParId.IntegerValue == (int)BuiltInParameter.PHASE_CREATED
                || filterData.ParId.IntegerValue == (int)BuiltInParameter.SCHEDULE_LEVEL_PARAM)
#endif
            {
                FilterNumericRuleEvaluator re = null;
                switch (filterData.FilterType)
                {
                    case ScheduleFilterType.Equal: re = new FilterNumericEquals(); break;
                    case ScheduleFilterType.NotEqual: re = new FilterNumericEquals(); break;
                    case ScheduleFilterType.GreaterThan: re = new FilterNumericGreater(); break;
                    case ScheduleFilterType.GreaterThanOrEqual: re = new FilterNumericGreaterOrEqual(); break;
                    case ScheduleFilterType.LessThan: re = new FilterNumericLess(); break;
                    case ScheduleFilterType.LessThanOrEqual: re = new FilterNumericLessOrEqual(); break;
                }
                if (re != null)
                {
                    ParameterValueProvider pvp = new ParameterValueProvider(filterData.ParId);
                    if (filterData.Value is double) filterRule = new FilterDoubleRule(pvp, re, (double)filterData.Value, double.Epsilon);
                    else if (filterData.Value is ElementId) filterRule = new FilterElementIdRule(pvp, re, (ElementId)filterData.Value);
                    else if (filterData.Value is int) filterRule = new FilterIntegerRule(pvp, re, (int)filterData.Value);
                }
            }
            //else if (filterData.Value != null)
            else if (filterData.Filter.IsStringValue || filterData.Filter.IsElementIdValue)
            {
                FilterStringRuleEvaluator re = null;
                switch (filterData.FilterType)
                {
                    case ScheduleFilterType.BeginsWith: re = new FilterStringBeginsWith(); break;
                    case ScheduleFilterType.NotBeginsWith: re = new FilterStringBeginsWith(); break;
                    case ScheduleFilterType.Contains: re = new FilterStringContains(); break;
                    case ScheduleFilterType.NotContains: re = new FilterStringContains(); break;
                    case ScheduleFilterType.EndsWith: re = new FilterStringEndsWith(); break;
                    case ScheduleFilterType.NotEndsWith: re = new FilterStringEndsWith(); break;
                    case ScheduleFilterType.Equal: re = new FilterStringEquals(); break;
                    case ScheduleFilterType.NotEqual: re = new FilterStringEquals(); break;
                    case ScheduleFilterType.GreaterThan:
#if REVIT2024 || REVIT2025
                        if (string.IsNullOrEmpty(filterData.Value.ToString())) filterRule = new HasValueFilterRule(filterData.ParId);
                        else re = new FilterStringGreater(); 
#else
                        re = new FilterStringGreater();
#endif
                        break;

                    case ScheduleFilterType.GreaterThanOrEqual:
#if REVIT2024 || REVIT2025
                        if (string.IsNullOrEmpty(filterData.Value.ToString())) filterRule = new HasValueFilterRule(filterData.ParId);
                        else re = new FilterStringGreaterOrEqual();
#else
                        re = new FilterStringGreaterOrEqual();
#endif
                        break;

                    case ScheduleFilterType.LessThan:
                        //if (string.IsNullOrEmpty(filterData.Value.ToString())) filterRule = new HasNoValueFilterRule(filterData.ParId);
                        //else re = new FilterStringLess(); 
                        re = new FilterStringLess();
                        break;

                    case ScheduleFilterType.LessThanOrEqual:
                        //if (string.IsNullOrEmpty(filterData.Value.ToString())) filterRule = new HasNoValueFilterRule(filterData.ParId);
                        //else re = new FilterStringLessOrEqual();
                        re = new FilterStringLessOrEqual();
                        break;
                }
                if (re != null)
                {
                    ParameterValueProvider pvp = new ParameterValueProvider(filterData.ParId);
#if REVIT2023 || REVIT2024 || REVIT2025
                    filterRule = new FilterStringRule(pvp, re, filterData.Value.ToString());
#else
                    filterRule = new FilterStringRule(pvp, re, filterData.Value.ToString(), true);
#endif
                }
            }
            if (filterRule != null && filterData.FilterType.ToString().Contains("Not")) filterRule = new FilterInverseRule(filterRule);
            else if (filterRule == null)
            {
#if REVIT2024 || REVIT2025
                switch (filterData.FilterType) 
                {
                    case ScheduleFilterType.HasValue: filterRule = new HasValueFilterRule(filterData.ParId); break;
                    case ScheduleFilterType.HasNoValue: filterRule = new HasNoValueFilterRule(filterData.ParId); break;
                }
#endif
            }
            return filterRule;
        }
    }
}

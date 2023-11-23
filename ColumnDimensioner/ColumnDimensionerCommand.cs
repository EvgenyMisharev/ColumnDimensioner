using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ColumnDimensioner
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class ColumnDimensionerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                GetPluginStartInfo();
            }
            catch { }

            Document doc = commandData.Application.ActiveUIDocument.Document;
            Selection sel = commandData.Application.ActiveUIDocument.Selection;
            Autodesk.Revit.DB.View activeView = doc.ActiveView;

            if (activeView.ViewType != ViewType.FloorPlan && activeView.ViewType != ViewType.EngineeringPlan)
            {
                TaskDialog.Show("Revit", "Перед запуском плагина откройте \"План этажа\" или \"План несущих конструкций\"");
                return Result.Cancelled;
            }

            List<DimensionType> dimensionTypesList = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .ToList()
                .Where(dt => dt.StyleType == DimensionStyleType.Linear)
                .Where(dt => dt.Name != "Стиль линейных размеров")
                .OrderBy(dt => dt.Name, new AlphanumComparatorFastString())
                .ToList();

            if (dimensionTypesList.Count == 0)
            {
                TaskDialog.Show("Revit", "Не удалось найти ни одного типа для линейных размеров!");
                return Result.Cancelled;
            }

            ColumnDimensionerWPF columnDimensionerWPF = new ColumnDimensionerWPF(dimensionTypesList);
            columnDimensionerWPF.ShowDialog();
            if (columnDimensionerWPF.DialogResult != true)
            {
                return Result.Cancelled;
            }

            List<FamilyInstance> columnsList = new List<FamilyInstance>();
            if (columnDimensionerWPF.DimensionColumnsButtonName == "radioButton_VisibleInView")
            {
                columnsList = new FilteredElementCollector(doc, activeView.Id)
                    .OfCategory(BuiltInCategory.OST_StructuralColumns)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(c => c.HandOrientation != null)
                    .ToList();
            }
            else
            {
                columnsList = GetColumnsFromCurrentSelection(doc, sel);
                if (columnsList.Count == 0)
                {
                    ColumnSelectionFilter columnSelFilter = new ColumnSelectionFilter();
                    IList<Reference> selColumnsReferenceList = null;
                    try
                    {
                        selColumnsReferenceList = sel.PickObjects(ObjectType.Element, columnSelFilter, "Выберите несущие колонны!");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        return Result.Cancelled;
                    }

                    foreach (Reference columnRef in selColumnsReferenceList)
                    {
                        columnsList.Add(doc.GetElement(columnRef) as FamilyInstance);
                    }
                }
            }

            if (columnsList.Count == 0)
            {
                TaskDialog.Show("Revit", "Не выбрано ни одной колонны!");
                return Result.Cancelled;
            }

            DimensionType dimensionType = columnDimensionerWPF.SelectedDimensionType;
            bool indentationFirstRowDimensionsIsChecked = columnDimensionerWPF.IndentationFirstRowDimensionsIsChecked;
            double indentationFirstRowDimensions = 0;
            string indentationFirstRowDimensionsSTR = columnDimensionerWPF.IndentationFirstRowDimensions;
            double.TryParse(indentationFirstRowDimensionsSTR, out indentationFirstRowDimensions);
            indentationFirstRowDimensions = indentationFirstRowDimensions / 304.8;

            bool IndentationSecondRowDimensionsIsChecked = columnDimensionerWPF.IndentationSecondRowDimensionsIsChecked;
            double indentationSecondRowDimensions = 0;
            string indentationSecondRowDimensionsSTR = columnDimensionerWPF.IndentationSecondRowDimensions;
            double.TryParse(indentationSecondRowDimensionsSTR, out indentationSecondRowDimensions);
            indentationSecondRowDimensions = indentationSecondRowDimensions / 304.8;

            int scale = doc.ActiveView.get_Parameter(BuiltInParameter.VIEW_SCALE_PULLDOWN_METRIC).AsInteger();

            List<Grid> gridsList = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfCategory(BuiltInCategory.OST_Grids)
                .Cast<Grid>()
                .ToList();

            Options opt = new Options();
            opt.ComputeReferences = true;
            opt.IncludeNonVisibleObjects = false;
            opt.View = doc.ActiveView;

            using (TransactionGroup transactionGroup = new TransactionGroup(doc))
            {
                transactionGroup.Start("Образмерить колонны");

                TextNoteType newTextNoteType = null;
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Создать новый тип текста под размер");
                    newTextNoteType = new FilteredElementCollector(doc)
                        .OfClass(typeof(TextNoteType))
                        .WhereElementIsElementType()
                        .Cast<TextNoteType>()
                        .FirstOrDefault(tnt => tnt.Name == "tmpTT");

                    if (newTextNoteType == null)
                    {
                        TextNoteType existingTextNoteType = new FilteredElementCollector(doc)
                            .OfClass(typeof(TextNoteType))
                            .FirstElement() as TextNoteType;

                        string fontName = dimensionType.get_Parameter(BuiltInParameter.TEXT_FONT).AsString();
                        double textSize = dimensionType.get_Parameter(BuiltInParameter.TEXT_SIZE).AsDouble() * 304.8;
                        int styleBold = dimensionType.get_Parameter(BuiltInParameter.TEXT_STYLE_BOLD).AsInteger();
                        int styleItalic = dimensionType.get_Parameter(BuiltInParameter.TEXT_STYLE_ITALIC).AsInteger();
                        int styleUnderline = dimensionType.get_Parameter(BuiltInParameter.TEXT_STYLE_UNDERLINE).AsInteger();
                        double styleWidthScale = dimensionType.get_Parameter(BuiltInParameter.TEXT_WIDTH_SCALE).AsDouble();

                        newTextNoteType = existingTextNoteType.Duplicate("tmpTT") as TextNoteType;
                        newTextNoteType.get_Parameter(BuiltInParameter.TEXT_FONT).Set(fontName);
#if R2019 || R2020 || R2021
                        newTextNoteType.get_Parameter(BuiltInParameter.TEXT_SIZE).Set(UnitUtils.ConvertToInternalUnits(textSize, DisplayUnitType.DUT_MILLIMETERS));
#else
                        newTextNoteType.get_Parameter(BuiltInParameter.TEXT_SIZE).Set(UnitUtils.ConvertToInternalUnits(textSize, UnitTypeId.Millimeters));
#endif
                        newTextNoteType.get_Parameter(BuiltInParameter.TEXT_STYLE_BOLD).Set(styleBold);
                        newTextNoteType.get_Parameter(BuiltInParameter.TEXT_STYLE_ITALIC).Set(styleItalic);
                        newTextNoteType.get_Parameter(BuiltInParameter.TEXT_STYLE_UNDERLINE).Set(styleUnderline);
                        newTextNoteType.get_Parameter(BuiltInParameter.TEXT_WIDTH_SCALE).Set(styleWidthScale);
                    }
                    t.Commit();
                }

                foreach (FamilyInstance column in columnsList)
                {
                    XYZ columnLocationPoint = (column.Location as LocationPoint).Point;
                    XYZ columnHandOrientation = column.HandOrientation;
                    XYZ columnFacingOrientation = column.FacingOrientation;

                    double columnWeight = 0;
                    double columnHeight = 0;
                    CurveArray columnProfileCurveArray = column.GetSweptProfile().GetSweptProfile().Curves;

                    foreach (Line curve in columnProfileCurveArray)
                    {
                        if (curve.Direction.IsAlmostEqualTo(XYZ.BasisX))
                        {
                            columnWeight = curve.Length;
                        }
                        else if (curve.Direction.IsAlmostEqualTo(XYZ.BasisY))
                        {
                            columnHeight = curve.Length;
                        }
                    }


                    //Линия вдоль HandOrientation
                    XYZ handOrientationCurvePStart = columnLocationPoint + columnWeight / 2 * columnHandOrientation.Negate();
                    XYZ handOrientationCurvePEnd = columnLocationPoint + columnWeight / 2 * columnHandOrientation;
                    Curve handOrientationCurve = Line.CreateBound(handOrientationCurvePStart, handOrientationCurvePEnd);

                    //Сначала общие размеры HandOrientation
                    ReferenceArray referenceArrayHandOrientation = new ReferenceArray();
                    List<Face> handOrientationFaces = GetFaceListFromColumnSolid(opt, column, 0);
                    foreach (Face face in handOrientationFaces)
                    {
                        referenceArrayHandOrientation.Append(face.Reference);
                    }

                    if (IndentationSecondRowDimensionsIsChecked)
                    {
                        Dimension mainDimensionHandOrientation = null;
                        using (Transaction t = new Transaction(doc))
                        {
                            t.Start("Размер вдоль HandOrientation");
                            XYZ mainDimensionTranslationHandOrientation = columnHeight * columnFacingOrientation.Negate() + indentationSecondRowDimensions * columnFacingOrientation.Negate();
                            try
                            {
                                mainDimensionHandOrientation = doc.Create.NewDimension(doc.ActiveView, handOrientationCurve as Line, referenceArrayHandOrientation, dimensionType);
                            }
                            catch
                            {
                                continue;
                            }
                            ElementTransformUtils.MoveElement(doc, mainDimensionHandOrientation.Id, mainDimensionTranslationHandOrientation);
                            t.Commit();
                        }
                        string mainDimensionTextHandOrientation = mainDimensionHandOrientation.get_Parameter(BuiltInParameter.DIM_VALUE_LENGTH).AsValueString();

                        //Создать текстовое примечание
                        double mainDimensionNewWidthHandOrientation = 0;
                        TextNote textNoteHandOrientation = null;
                        using (Transaction t = new Transaction(doc))
                        {
                            t.Start("Размер текстового примечания");
                            textNoteHandOrientation = TextNote.Create(doc, activeView.Id, new XYZ(0, 0, 0), mainDimensionTextHandOrientation, newTextNoteType.Id);
                            t.Commit();
                        }

                        using (Transaction t = new Transaction(doc))
                        {
                            t.Start("Удаление примечания");
                            mainDimensionNewWidthHandOrientation = textNoteHandOrientation.Width * scale;
                            doc.Delete(textNoteHandOrientation.Id);
                            t.Commit();
                        }

                        using (Transaction t = new Transaction(doc))
                        {
                            t.Start("Перемещение размера");
                            if (mainDimensionNewWidthHandOrientation > handOrientationCurve.Length)
                            {
                                mainDimensionHandOrientation.TextPosition = mainDimensionHandOrientation.TextPosition
                                    + handOrientationCurve.Length / 2 * columnHandOrientation
                                    + mainDimensionNewWidthHandOrientation / 2 * columnHandOrientation
                                    + 2 / 304.8 * scale * columnHandOrientation;
                            }
                            t.Commit();
                        }
                    }

                    if (indentationFirstRowDimensionsIsChecked)
                    {
                        //Найти ось пересекающую колонну перпендикулярно HandOrientation
                        bool flagHandOrientation = false;
                        foreach (Grid grid in gridsList)
                        {
                            if (grid.Curve as Line != null)
                            {
                                if ((grid.Curve as Line).Direction.IsAlmostEqualTo(columnFacingOrientation)
                                     || (grid.Curve as Line).Direction.IsAlmostEqualTo(columnFacingOrientation.Negate()))
                                {
                                    foreach (GeometryObject obj in grid.get_Geometry(opt))
                                    {
                                        SetComparisonResult intersectionResult = grid.Curve.Intersect(handOrientationCurve);
                                        if (intersectionResult == SetComparisonResult.Overlap)
                                        {
                                            if (obj is Line)
                                            {
                                                Line gridLine = obj as Line;
                                                referenceArrayHandOrientation.Append(gridLine.Reference);
                                                flagHandOrientation = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                                if (flagHandOrientation) break;
                            }
                        }

                        if (flagHandOrientation)
                        {
                            bool dSide = true;
                            Dimension secondDimensionHandOrientation = null;
                            using (Transaction t = new Transaction(doc))
                            {
                                t.Start("Размер вдоль HandOrientation второстепенный");
                                XYZ secondDimensionTranslationHandOrientation = columnHeight * columnFacingOrientation.Negate() + indentationFirstRowDimensions * columnFacingOrientation.Negate();
                                secondDimensionHandOrientation = doc.Create.NewDimension(doc.ActiveView, handOrientationCurve as Line, referenceArrayHandOrientation, dimensionType);
                                ElementTransformUtils.MoveElement(doc, secondDimensionHandOrientation.Id, secondDimensionTranslationHandOrientation);
                                t.Commit();
                            }

                            DimensionSegmentArray secondDimensionHandOrientationSegmentArray = secondDimensionHandOrientation.Segments;
                            foreach (DimensionSegment dimensionSegment in secondDimensionHandOrientationSegmentArray)
                            {
                                //Создать текстовое примечание
                                double dimensionSegmentValue = (double)dimensionSegment.Value;
                                string secondDimensionTextHandOrientation = dimensionSegment.ValueString;
                                double secondDimensionNewWidthHandOrientation = 0;
                                TextNote secondTextNote = null;
                                using (Transaction t = new Transaction(doc))
                                {
                                    t.Start("Размер текстового примечания");
                                    secondTextNote = TextNote.Create(doc, activeView.Id, new XYZ(0, 0, 0), secondDimensionTextHandOrientation, newTextNoteType.Id);
                                    t.Commit();
                                }

                                using (Transaction t = new Transaction(doc))
                                {
                                    t.Start("Удаление примечания");
                                    secondDimensionNewWidthHandOrientation = secondTextNote.Width * scale;
                                    doc.Delete(secondTextNote.Id);
                                    t.Commit();
                                }

                                using (Transaction t = new Transaction(doc))
                                {
                                    t.Start("Перемещение размера");
                                    if (secondDimensionNewWidthHandOrientation > dimensionSegment.Value)
                                    {
                                        if (dSide)
                                        {
                                            dimensionSegment.TextPosition = dimensionSegment.TextPosition
                                                + dimensionSegmentValue / 2 * columnHandOrientation.Negate()
                                                + secondDimensionNewWidthHandOrientation / 2 * columnHandOrientation.Negate()
                                                + 2 / 304.8 * scale * columnHandOrientation.Negate();
                                            dSide = !dSide;
                                            t.Commit();
                                            continue;
                                        }
                                        if (!dSide)
                                        {
                                            dimensionSegment.TextPosition = dimensionSegment.TextPosition
                                                + dimensionSegmentValue / 2 * columnHandOrientation
                                                + secondDimensionNewWidthHandOrientation / 2 * columnHandOrientation
                                                + 2 / 304.8 * scale * columnHandOrientation;
                                            dSide = !dSide;
                                            t.Commit();
                                            continue;
                                        }
                                    }
                                    t.Commit();
                                }
                            }
                        }
                    }

                    //Линия вдоль FacingOrientation
                    XYZ facingOrientationCurvePStart = columnLocationPoint + columnWeight / 2 * columnFacingOrientation.Negate();
                    XYZ facingOrientationCurvePEnd = columnLocationPoint + columnWeight / 2 * columnFacingOrientation;
                    Curve facingOrientationCurve = Line.CreateBound(facingOrientationCurvePStart, facingOrientationCurvePEnd);

                    //Сначала общие размеры FacingOrientation
                    ReferenceArray referenceArrayFacingOrientation = new ReferenceArray();
                    List<Face> facingOrientationFaces = GetFaceListFromColumnSolid(opt, column, 1);
                    foreach (Face face in facingOrientationFaces)
                    {
                        referenceArrayFacingOrientation.Append(face.Reference);
                    }

                    if (IndentationSecondRowDimensionsIsChecked)
                    {
                        Dimension mainDimensionFacingOrientation = null;
                        using (Transaction t = new Transaction(doc))
                        {
                            t.Start("Размер вдоль HandOrientation");
                            XYZ mainDimensionTranslationFacingOrientation = columnWeight * columnHandOrientation + indentationSecondRowDimensions * columnHandOrientation;
                            try
                            {
                                mainDimensionFacingOrientation = doc.Create.NewDimension(doc.ActiveView, facingOrientationCurve as Line, referenceArrayFacingOrientation, dimensionType);
                            }
                            catch
                            {
                                continue;
                            }

                            ElementTransformUtils.MoveElement(doc, mainDimensionFacingOrientation.Id, mainDimensionTranslationFacingOrientation);
                            t.Commit();
                        }
                        string mainDimensionTextFacingOrientation = mainDimensionFacingOrientation.get_Parameter(BuiltInParameter.DIM_VALUE_LENGTH).AsValueString();

                        //Создать текстовое примечание
                        double mainDimensionNewWidthFacingOrientation = 0;
                        TextNote textNoteFacingOrientation = null;
                        using (Transaction t = new Transaction(doc))
                        {
                            t.Start("Размер текстового примечания");
                            textNoteFacingOrientation = TextNote.Create(doc, activeView.Id, new XYZ(0, 0, 0), mainDimensionTextFacingOrientation, newTextNoteType.Id);
                            t.Commit();
                        }

                        using (Transaction t = new Transaction(doc))
                        {
                            t.Start("Удаление примечания");
                            mainDimensionNewWidthFacingOrientation = textNoteFacingOrientation.Width * scale;
                            doc.Delete(textNoteFacingOrientation.Id);
                            t.Commit();
                        }

                        using (Transaction t = new Transaction(doc))
                        {
                            t.Start("Перемещение размера");
                            if (mainDimensionNewWidthFacingOrientation > facingOrientationCurve.Length)
                            {
                                mainDimensionFacingOrientation.TextPosition = mainDimensionFacingOrientation.TextPosition
                                    + facingOrientationCurve.Length / 2 * columnFacingOrientation
                                    + mainDimensionNewWidthFacingOrientation / 2 * columnFacingOrientation
                                    + 2 / 304.8 * scale * columnFacingOrientation;
                            }
                            t.Commit();
                        }
                    }

                    if (indentationFirstRowDimensionsIsChecked)
                    {
                        //Найти ось пересекающую колонну перпендикулярно FacingOrientation
                        bool flagFacingOrientation = false;
                        foreach (Grid grid in gridsList)
                        {
                            if (grid.Curve as Line != null)
                            {
                                if ((grid.Curve as Line).Direction.IsAlmostEqualTo(columnHandOrientation)
                                     || (grid.Curve as Line).Direction.IsAlmostEqualTo(columnHandOrientation.Negate()))
                                {
                                    foreach (GeometryObject obj in grid.get_Geometry(opt))
                                    {
                                        SetComparisonResult intersectionResult = grid.Curve.Intersect(facingOrientationCurve);
                                        if (intersectionResult == SetComparisonResult.Overlap)
                                        {
                                            if (obj is Line)
                                            {
                                                Line gridLine = obj as Line;
                                                referenceArrayFacingOrientation.Append(gridLine.Reference);
                                                flagFacingOrientation = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                                if (flagFacingOrientation) break;
                            }
                        }

                        if (flagFacingOrientation)
                        {
                            bool dSide = true;
                            Dimension secondDimensionFacingOrientation = null;
                            using (Transaction t = new Transaction(doc))
                            {
                                t.Start("Размер вдоль HandOrientation второстепенный");
                                XYZ secondDimensionTranslationFacingOrientation = columnWeight * columnHandOrientation + indentationFirstRowDimensions * columnHandOrientation;
                                secondDimensionFacingOrientation = doc.Create.NewDimension(doc.ActiveView, facingOrientationCurve as Line, referenceArrayFacingOrientation, dimensionType);
                                ElementTransformUtils.MoveElement(doc, secondDimensionFacingOrientation.Id, secondDimensionTranslationFacingOrientation);
                                t.Commit();
                            }

                            DimensionSegmentArray secondDimensioFacingOrientationSegmentArray = secondDimensionFacingOrientation.Segments;
                            foreach (DimensionSegment dimensionSegment in secondDimensioFacingOrientationSegmentArray)
                            {
                                //Создать текстовое примечание
                                double dimensionSegmentValue = (double)dimensionSegment.Value;
                                string secondDimensionTextFacingOrientation = dimensionSegment.ValueString;
                                double secondDimensionNewWidthFacingOrientation = 0;
                                TextNote secondTextNote = null;
                                using (Transaction t = new Transaction(doc))
                                {
                                    t.Start("Размер текстового примечания");
                                    secondTextNote = TextNote.Create(doc, activeView.Id, new XYZ(0, 0, 0), secondDimensionTextFacingOrientation, newTextNoteType.Id);
                                    t.Commit();
                                }

                                using (Transaction t = new Transaction(doc))
                                {
                                    t.Start("Удаление примечания");
                                    secondDimensionNewWidthFacingOrientation = secondTextNote.Width * scale;
                                    doc.Delete(secondTextNote.Id);
                                    t.Commit();
                                }

                                using (Transaction t = new Transaction(doc))
                                {
                                    t.Start("Перемещение размера");
                                    if (secondDimensionNewWidthFacingOrientation > dimensionSegment.Value)
                                    {
                                        if (dSide)
                                        {
                                            dimensionSegment.TextPosition = dimensionSegment.TextPosition
                                                + dimensionSegmentValue / 2 * columnFacingOrientation.Negate()
                                                + secondDimensionNewWidthFacingOrientation / 2 * columnFacingOrientation.Negate()
                                                + 2 / 304.8 * scale * columnFacingOrientation.Negate();
                                            dSide = !dSide;
                                            t.Commit();
                                            continue;
                                        }
                                        if (!dSide)
                                        {
                                            dimensionSegment.TextPosition = dimensionSegment.TextPosition
                                                + dimensionSegmentValue / 2 * columnFacingOrientation
                                                + secondDimensionNewWidthFacingOrientation / 2 * columnFacingOrientation
                                                + 2 / 304.8 * scale * columnFacingOrientation;
                                            dSide = !dSide;
                                            t.Commit();
                                            continue;
                                        }
                                    }
                                    t.Commit();
                                }
                            }
                        }
                    }
                }

                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Удалить временный тип текста");
                    if (newTextNoteType != null)
                    {
                        doc.Delete(newTextNoteType.Id);
                    }
                    t.Commit();
                }

                transactionGroup.Assimilate();
            }
            return Result.Succeeded;
        }
        private static List<Face> GetFaceListFromColumnSolid(Options opt, FamilyInstance column, int dir)
        {
            List<Face> facesList = new List<Face>();
            GeometryElement geomElem = column.get_Geometry(opt);

            foreach (GeometryObject geoObject in geomElem)
            {
                GeometryInstance instance = geoObject as GeometryInstance;
                if (null != instance)
                {
                    Transform transform = instance.Transform;
                    if (instance.GetSymbolGeometry().Count() != 0)
                    {
                        foreach (GeometryObject instObj in instance.GetSymbolGeometry())
                        {
                            Solid solid = instObj as Solid;
                            if (null == solid || 0 == solid.Faces.Size || 0 == solid.Edges.Size)
                            {
                                continue;
                            }
                            foreach (Face face in solid.Faces)
                            {
                                UV uV = new UV(0.5, 0.5);
                                if (dir == 0)
                                {
                                    if (transform.OfVector(face.ComputeNormal(uV)).IsAlmostEqualTo(column.HandOrientation)
                                        || transform.OfVector(face.ComputeNormal(uV)).IsAlmostEqualTo(column.HandOrientation.Negate()))
                                    {
                                        facesList.Add(face);
                                    }
                                }
                                else if (dir == 1)
                                {
                                    if (transform.OfVector(face.ComputeNormal(uV)).IsAlmostEqualTo(column.FacingOrientation)
                                        || transform.OfVector(face.ComputeNormal(uV)).IsAlmostEqualTo(column.FacingOrientation.Negate()))
                                    {
                                        facesList.Add(face);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (GeometryObject instObj in geomElem)
                        {
                            Solid solid = instObj as Solid;
                            if (null == solid || 0 == solid.Faces.Size || 0 == solid.Edges.Size)
                            {
                                continue;
                            }
                            foreach (Face face in solid.Faces)
                            {
                                UV uV = new UV(0.5, 0.5);
                                if (dir == 0)
                                {
                                    if (face.ComputeNormal(uV).IsAlmostEqualTo(column.HandOrientation)
                                        || face.ComputeNormal(uV).IsAlmostEqualTo(column.HandOrientation.Negate()))
                                    {
                                        if (facesList.FirstOrDefault(f => f.ComputeNormal(uV).IsAlmostEqualTo(face.ComputeNormal(uV))) == null)
                                        {
                                            facesList.Add(face);
                                        }
                                    }
                                }
                                else if (dir == 1)
                                {
                                    if (face.ComputeNormal(uV).IsAlmostEqualTo(column.FacingOrientation)
                                        || face.ComputeNormal(uV).IsAlmostEqualTo(column.FacingOrientation.Negate()))
                                    {
                                        if (facesList.FirstOrDefault(f => f.ComputeNormal(uV).IsAlmostEqualTo(face.ComputeNormal(uV))) == null)
                                        {
                                            facesList.Add(face);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return facesList;
        }
        private static List<FamilyInstance> GetColumnsFromCurrentSelection(Document doc, Selection sel)
        {
            ICollection<ElementId> selectedIds = sel.GetElementIds();
            List<FamilyInstance> tempColumnsList = new List<FamilyInstance>();
            foreach (ElementId columnId in selectedIds)
            {
                if (doc.GetElement(columnId) is FamilyInstance
                    && null != doc.GetElement(columnId).Category
                    && doc.GetElement(columnId).Category.Id.IntegerValue.Equals((int)BuiltInCategory.OST_StructuralColumns))
                {
                    tempColumnsList.Add(doc.GetElement(columnId) as FamilyInstance);
                }
            }
            return tempColumnsList;
        }
        private static void GetPluginStartInfo()
        {
            // Получаем сборку, в которой выполняется текущий код
            Assembly thisAssembly = Assembly.GetExecutingAssembly();
            string assemblyName = "ColumnDimensioner";
            string assemblyNameRus = "Образмерить колонны";
            string assemblyFolderPath = Path.GetDirectoryName(thisAssembly.Location);

            int lastBackslashIndex = assemblyFolderPath.LastIndexOf("\\");
            string dllPath = assemblyFolderPath.Substring(0, lastBackslashIndex + 1) + "PluginInfoCollector\\PluginInfoCollector.dll";

            Assembly assembly = Assembly.LoadFrom(dllPath);
            Type type = assembly.GetType("PluginInfoCollector.InfoCollector");
            var constructor = type.GetConstructor(new Type[] { typeof(string), typeof(string) });

            if (type != null)
            {
                // Создание экземпляра класса
                object instance = Activator.CreateInstance(type, new object[] { assemblyName, assemblyNameRus });
            }
        }
    }
}

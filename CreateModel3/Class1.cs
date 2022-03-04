using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;

namespace CreateModel3
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            var level1 = FoundLevel(doc, "Уровень 1");
            var level2 = FoundLevel(doc, "Уровень 2");
            List<Wall> walls = CreateWall(doc, level1, level2);
            AddDoor(doc, level1, walls[0]);
            AddWindow(doc, level1, walls[1], 600);
            AddWindow(doc, level1, walls[2], 800);
            AddWindow(doc, level1, walls[3], 800);
            CreateRoof(doc, level2, walls);

            return Result.Succeeded;
        }

       
        private void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 2134 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            Transaction transaction = new Transaction(doc, "Создание двери");
            transaction.Start();
            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!doorType.IsActive)
                doorType.Activate();

            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);
            transaction.Commit();
        }

        
        private void AddWindow(Document doc, Level level1, Wall wall, double offset)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0406 x 0610 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();

            Transaction transaction = new Transaction(doc, "Создание окна");
            transaction.Start();
            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!windowType.IsActive)
                windowType.Activate();

            var window = doc.Create.NewFamilyInstance(point, windowType, wall, level1, StructuralType.NonStructural);
            double offsetMil = UnitUtils.ConvertToInternalUnits(offset, UnitTypeId.Millimeters);
            window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(offsetMil);
            transaction.Commit();
        }

        
        public Level FoundLevel(Document doc, string str)
        {
            List<Level> listLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();

            var level = listLevel
                .Where(x => x.Name.Equals(str))
                .FirstOrDefault();

            return level;
        }

        
        public List<Wall> CreateWall(Document doc, Level lev1, Level lev2)
        {

            var level1 = lev1;
            var level2 = lev2;
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);
            double dx = width / 2;
            double dy = depth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            List<Wall> wallList = new List<Wall>();

            Transaction transaction = new Transaction(doc, "Создание стен");
            transaction.Start();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                wallList.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }
            transaction.Commit();
            return wallList;
        }

        //метод создания крыши
        public void CreateRoof(Document doc, Level level2, List<Wall> wallList)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();


            LocationCurve hostCurve = wallList[1].Location as LocationCurve;
            XYZ point3 = hostCurve.Curve.GetEndPoint(0);
            XYZ point4 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point3 + point4) / 2;

            double wallWigth = wallList[0].Width;
            double dt = wallWigth / 2;

            XYZ point1 = new XYZ(-dt, -dt, level2.Elevation);
            XYZ point2 = new XYZ(dt, dt, level2.Elevation); 

            XYZ A = point3 + point1;
            XYZ B = new XYZ(point.X, point.Y, 20);
            XYZ C = point4 + point2;


            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(A, B));
            curveArray.Append(Line.CreateBound(B, C));
            using (Transaction tr = new Transaction(doc))
            {
                tr.Start("Create ExtrusionRoof");
                ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
                var roof = doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, -A.X - wallWigth, A.X + wallWigth);
                roof.get_Parameter(BuiltInParameter.ROOF_EAVE_CUT_PARAM).Set(33619);
                tr.Commit();
            }
        }
    }
}

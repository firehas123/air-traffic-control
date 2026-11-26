using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>The painted Island map (owner's picture, 20 Nov 2026): camera framing and level data fitted to the picture.</summary>
    public class CameraFramingTests
    {
        static readonly Rect Picture = new Rect(-6.6713f, -5f, 13.3425f, 10f); // 1449 x 1086 at 108.6 px per unit
        static readonly Rect Airport = new Rect(-6.165f, -1.4f, 12.385f, 5.848f);

        static void AssertInside(Rect inner, Rect outer, string what)
        {
            Assert.LessOrEqual(outer.xMin, inner.xMin + 1e-3f, what);
            Assert.GreaterOrEqual(outer.xMax, inner.xMax - 1e-3f, what);
            Assert.LessOrEqual(outer.yMin, inner.yMin + 1e-3f, what);
            Assert.GreaterOrEqual(outer.yMax, inner.yMax - 1e-3f, what);
        }

        [Test]
        public void IPad_ShowsTheWholePicture()
        {
            Rect view = CameraFraming.View(Picture, Airport, 2732f / 2048f);
            Assert.AreEqual(10f, view.height, 0.01f, "The island fills the screen");
            Assert.AreEqual(Picture.width, view.width, 0.02f);
            AssertInside(view, Picture, "Nothing past the picture's edges");
        }

        [TestCase(16f / 9f)]
        [TestCase(19.5f / 9f)]
        [TestCase(2532f / 1170f)]
        public void WiderScreens_CropSeaNeverTheAirport(float aspect)
        {
            Rect view = CameraFraming.View(Picture, Airport, aspect);
            AssertInside(Airport, view, "The airport is always on screen");
            AssertInside(view, Picture, "Only picture is shown, no empty space");
            Assert.Less(view.height, Picture.height, "Some sea is cropped");
        }

        [Test]
        public void ExtremeScreen_StillKeepsTheAirport()
        {
            Rect view = CameraFraming.View(Picture, Airport, 3.5f);
            AssertInside(Airport, view, "The airport wins, even if the view must go past the picture");
        }

        [Test]
        public void PixelAndWorld_RoundTrip()
        {
            var size = new Vector2(1449f, 1086f);
            Vector2 world = CameraFraming.PixelToWorld(new Vector2(293f, 233f), size, 108.6f);
            Vector2 back = CameraFraming.WorldToPixel(world, size, 108.6f);
            Assert.AreEqual(293f, back.x, 1e-3f);
            Assert.AreEqual(233f, back.y, 1e-3f);
            Assert.AreEqual(Vector2.zero, CameraFraming.PixelToWorld(size / 2f, size, 108.6f), "The picture is centred on the origin");
        }

        [Test]
        public void SpawnPoints_StayJustInsideTheScreen()
        {
            var view = new Rect(-6.67f, -1.7f, 13.34f, 6.15f); // a wide phone
            Vector2 p = SpawnRules.ClampToView(new Vector2(-6.5f, -2.8f), view, 0.35f);
            Assert.AreEqual(-6.32f, p.x, 1e-3f);
            Assert.AreEqual(-1.35f, p.y, 1e-3f);
        }
    }

    /// <summary>
    /// Every measured picture layout (blueprint 16, 25 Nov 2026) lands on its picture's pixels: runways on asphalt,
    /// touchdown zones on the white threshold stripes, pads on their white rings, stands, taxi routes and hold lines on
    /// paved ground, hold lines clear of their runway, crossings on the runway, and the camera keeps every landing place
    /// on a 4:3 iPad.
    /// </summary>
    public class PictureLayoutTests
    {
        const string LayoutsFolder = "Assets/Maps/Layouts";
        static readonly Dictionary<string, Color32[]> PixelCache = new Dictionary<string, Color32[]>();

        static IEnumerable<string> LayoutPaths()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:PictureLayout", new[] { LayoutsFolder }))
                yield return AssetDatabase.GUIDToAssetPath(guid);
        }

        static PictureLayout Load(string path) => AssetDatabase.LoadAssetAtPath<PictureLayout>(path);

        // The picture's own pixels, read from the PNG file (not the compressed texture), row 0 at the top.
        sealed class Picture
        {
            readonly Color32[] pixels;
            readonly int width;
            readonly int height;

            public Picture(PictureLayout layout)
            {
                string file = "Assets/Art/Maps/" + layout.pictureFile;
                if (!PixelCache.TryGetValue(file, out pixels))
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    Assert.IsTrue(texture.LoadImage(System.IO.File.ReadAllBytes(file)), file);
                    pixels = texture.GetPixels32();
                    PixelCache[file] = pixels;
                    Object.DestroyImmediate(texture);
                }
                width = (int)layout.pictureSize.x;
                height = (int)layout.pictureSize.y;
                Assert.AreEqual(width * height, pixels.Length, "The picture is the size the layout says");
            }

            public Color32 At(Vector2 p)
            {
                int x = Mathf.Clamp(Mathf.RoundToInt(p.x), 0, width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(p.y), 0, height - 1);
                return pixels[(height - 1 - y) * width + x];
            }

            public bool White(Vector2 p, int min = 185)
            {
                Color32 c = At(p);
                return c.r > min && c.g > min && c.b > min;
            }

            // Grey asphalt, concrete or white paint: little colour. Snow is excluded by its blue tint.
            public bool Paved(Vector2 p, int r)
            {
                int paved = 0, all = 0;
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        Color32 c = At(p + new Vector2(dx, dy));
                        int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                        bool snow = c.b - c.r > 12 && c.r > 165;
                        if (max - min < 32 && !snow) paved++;
                        all++;
                    }
                return paved * 2 >= all;
            }
        }

        static bool OnRunway(PictureRunway runway, Vector2 p, float margin)
        {
            Vector2 u = runway.Along, n = new Vector2(-u.y, u.x), d = p - runway.endA;
            float s = Vector2.Dot(d, u), o = Vector2.Dot(d, n);
            return s >= -margin && s <= runway.Length + margin && Mathf.Abs(o) <= runway.width / 2f + margin;
        }

        [Test]
        public void TenLayouts_ForTheFirstFiveMaps_BothModes()
        {
            var files = new List<string>();
            foreach (string path in LayoutPaths())
                files.Add(Load(path).pictureFile);
            foreach (string name in new[] { "01_island_tower", "02_island_draw", "03_city_tower", "04_city_draw", "05_desert_tower",
                                            "06_desert_draw", "07_mountain_tower", "08_mountain_draw", "09_arctic_tower", "10_arctic_draw" })
                CollectionAssert.Contains(files, name + ".png");
            foreach (string path in LayoutPaths())
            {
                PictureLayout layout = Load(path);
                Assert.AreEqual(layout.pictureFile.Contains("_tower") ? GameMode.Tower : GameMode.Draw, layout.mode, layout.pictureFile);
                Assert.IsNotNull(layout.picture, $"{layout.pictureFile} is linked (run the scene setup)");
                Assert.GreaterOrEqual(layout.runways.Count, 2, $"{layout.pictureFile}: every airport has crossing runways");
                Assert.AreEqual(1, layout.pads.FindAll(p => p.kind == LandingZoneKind.Helipad).Count, "One helipad");
                Assert.AreEqual(1, layout.pads.FindAll(p => p.kind == LandingZoneKind.BalloonField).Count, "One balloon field");
                Assert.GreaterOrEqual(layout.stands.Count, 4, "Stands");
            }
        }

        [Test]
        public void Runways_AreOnAsphalt_AndTouchdownZonesOnTheThresholdStripes()
        {
            foreach (string path in LayoutPaths())
            {
                PictureLayout layout = Load(path);
                var picture = new Picture(layout);
                foreach (PictureRunway runway in layout.runways)
                {
                    Vector2 u = runway.Along, n = new Vector2(-u.y, u.x);
                    foreach (float t in new[] { 0.2f, 0.35f, 0.65f, 0.8f })
                        Assert.IsTrue(picture.Paved(runway.endA + u * runway.Length * t + n * runway.width * 0.25f, 1),
                                      $"{layout.pictureFile} {runway.name} at {t}");
                    foreach (bool atA in new[] { true, false })
                    {
                        Vector2 zone = atA ? runway.zoneA : runway.zoneB;
                        Vector2 origin = atA ? runway.endA : runway.endB;
                        Vector2 along = atA ? u : -u;
                        int white = 0, all = 0;
                        for (float s = zone.x; s < zone.y; s += 1f)
                            for (int k = 0; k < 15; k++)
                            {
                                float off = Mathf.Lerp(-0.35f, 0.35f, k / 14f) * runway.width;
                                if (picture.White(origin + along * s + n * off)) white++;
                                all++;
                            }
                        Assert.GreaterOrEqual(white / (float)all, 0.3f, $"{layout.pictureFile} {runway.name} zone {(atA ? "A" : "B")} is on the white stripes");
                        Assert.Less(zone.x, 20f, "The zone starts at the runway end");
                    }

                    // The game's touchdown zone (shared by both ends) stays inside the stripes of each end.
                    RunwayDefinition definition = layout.ToRunwayDefinition(runway);
                    float start = definition.touchdownZoneStart * layout.pixelsPerUnit;
                    float end = start + definition.touchdownZoneLength * layout.pixelsPerUnit;
                    foreach (Vector2 zone in new[] { runway.zoneA, runway.zoneB })
                    {
                        Assert.GreaterOrEqual(start, zone.x - 0.5f);
                        Assert.LessOrEqual(end, zone.y + 0.5f);
                        Assert.GreaterOrEqual(end - start, 0.8f * (zone.y - zone.x), "Covers most of the stripes");
                    }
                    Assert.AreEqual(runway.Length, definition.length * layout.pixelsPerUnit, 0.5f);
                    Assert.AreEqual(runway.width, definition.width * layout.pixelsPerUnit, 0.5f);
                }
            }
        }

        [Test]
        public void Intersections_AreWhereTheRunwaysCross()
        {
            foreach (string path in LayoutPaths())
            {
                PictureLayout layout = Load(path);
                var picture = new Picture(layout);
                Assert.Greater(layout.intersections.Count, 0, layout.pictureFile);
                foreach (PictureIntersection x in layout.intersections)
                {
                    Assert.IsTrue(OnRunway(layout.FindRunway(x.runwayA), x.point, 0f) && OnRunway(layout.FindRunway(x.runwayB), x.point, 0f),
                                  $"{layout.pictureFile}: on both runways");
                    Assert.IsTrue(picture.Paved(x.point, 3));
                }
            }
        }

        [Test]
        public void Pads_SitOnTheirPaintedRings()
        {
            foreach (string path in LayoutPaths())
            {
                PictureLayout layout = Load(path);
                var picture = new Picture(layout);
                foreach (PicturePad pad in layout.pads)
                {
                    int hits = 0;
                    for (int i = 0; i < 48; i++)
                    {
                        float a = i * Mathf.PI * 2f / 48f;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        bool ring = false;
                        for (int dr = -3; dr <= 1; dr++)
                            ring |= picture.White(pad.centre + dir * (pad.radius + dr), 165);
                        if (ring) hits++;
                    }
                    Assert.GreaterOrEqual(hits, 29, $"{layout.pictureFile} {pad.kind}: {hits} of 48 ring samples");
                }
            }
        }

        [Test]
        public void MountainDraw_UsesTheLeftRingAsHelipad_WithAnH()
        {
            PictureLayout layout = Load(LayoutsFolder + "/08_mountain_draw.asset");
            PicturePad helipad = layout.pads.Find(p => p.kind == LandingZoneKind.Helipad);
            PicturePad field = layout.pads.Find(p => p.kind == LandingZoneKind.BalloonField);
            Assert.AreEqual(0.22f, helipad.centre.x / layout.pictureSize.x, 0.03f);
            Assert.AreEqual(0.62f, helipad.centre.y / layout.pictureSize.y, 0.03f);
            Assert.IsTrue(helipad.drawH, "An H is drawn on the plain ring");
            Assert.Greater(field.centre.x, helipad.centre.x, "The right ring is the balloon field");
            Assert.IsFalse(field.drawH);
        }

        [Test]
        public void StandsRoutesAndHolds_AreOnPavedGround_HoldsClearOfTheRunway()
        {
            foreach (string path in LayoutPaths())
            {
                PictureLayout layout = Load(path);
                var picture = new Picture(layout);
                foreach (PictureStand stand in layout.stands)
                    Assert.IsTrue(picture.Paved(stand.position, 3), $"{layout.pictureFile} {stand.name}");
                foreach (PicturePath route in layout.taxiRoutes)
                    for (int i = 1; i < route.points.Count; i++)
                        for (int k = 0; k <= 11; k++)
                        {
                            Vector2 p = Vector2.Lerp(route.points[i - 1], route.points[i], k / 11f);
                            Assert.IsTrue(picture.Paved(p, 2), $"{layout.pictureFile} route {route.name} at {p}");
                        }
                foreach (PictureHoldLine hold in layout.holdLines)
                {
                    PictureRunway runway = layout.FindRunway(hold.runway);
                    Assert.IsNotNull(runway, hold.runway);
                    Assert.IsTrue(picture.Paved(hold.point, 2), $"{layout.pictureFile} hold {hold.point}");
                    Assert.IsFalse(OnRunway(runway, hold.point, 2f), $"{layout.pictureFile} hold {hold.point} waits clear of {hold.runway}");
                    CollectionAssert.Contains(new[] { PictureHoldLine.EndA, PictureHoldLine.EndB, PictureHoldLine.Crossing }, hold.kind);
                }
                foreach (PictureCrossing crossing in layout.crossings)
                {
                    Assert.IsTrue(OnRunway(layout.FindRunway(crossing.runway), crossing.point, 0f), $"{layout.pictureFile} crossing on {crossing.runway}");
                    Assert.IsNotNull(layout.FindRoute(crossing.route), crossing.route);
                }
                Assert.IsTrue(layout.holdLines.Exists(h => h.kind == PictureHoldLine.EndA), $"{layout.pictureFile}: a hold line at a runway end");
            }
        }

        [Test]
        public void MountainAreas_CoverTheMountains_NotTheAirport()
        {
            foreach (string name in new[] { "07_mountain_tower", "08_mountain_draw" })
            {
                PictureLayout layout = Load($"{LayoutsFolder}/{name}.asset");
                List<PictureArea> mountains = layout.ruleAreas.FindAll(a => a.kind == PictureArea.Mountain);
                Assert.AreEqual(2, mountains.Count, "North and south");
                Assert.IsTrue(mountains.Exists(a => a.Contains(new Vector2(768f, 20f))), "Top edge is mountain");
                Assert.IsTrue(mountains.Exists(a => a.Contains(new Vector2(768f, 1010f))), "Bottom edge is mountain");
                foreach (PictureRunway runway in layout.runways)
                    foreach (PictureArea area in mountains)
                        Assert.IsFalse(area.Contains(runway.endA) || area.Contains(runway.endB), "Runways are in the valley");
                foreach (PicturePad pad in layout.pads)
                    foreach (PictureArea area in mountains)
                        Assert.IsFalse(area.Contains(pad.centre), "Pads are in the valley");
            }
        }

        [Test]
        public void IPad_ShowsTheAirportAndEveryLandingPlace_CroppingOnlyTheEdges()
        {
            foreach (string path in LayoutPaths())
            {
                PictureLayout layout = Load(path);
                Rect picture = layout.WorldRect;
                Rect keep = layout.KeepVisibleWorld;
                Rect view = CameraFraming.View(picture, keep, 4f / 3f);
                Assert.AreEqual(picture.height, view.height, 0.01f, $"{layout.pictureFile}: full height on the iPad");
                Assert.LessOrEqual(view.xMax, picture.xMax + 1e-3f, "Nothing past the picture");
                Assert.GreaterOrEqual(view.xMin, picture.xMin - 1e-3f);
                foreach (PictureRunway runway in layout.runways)
                    Assert.IsTrue(view.Contains(layout.World(runway.endA)) && view.Contains(layout.World(runway.endB)), $"{layout.pictureFile} {runway.name}");
                foreach (PicturePad pad in layout.pads)
                {
                    Vector2 c = layout.World(pad.centre);
                    float r = layout.Units(pad.radius) * 0.8f;
                    foreach (Vector2 d in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                        Assert.IsTrue(view.Contains(c + d * r), $"{layout.pictureFile} {pad.kind} is visible");
                }
                foreach (PictureStand stand in layout.stands)
                    Assert.IsTrue(view.Contains(layout.World(stand.position)), $"{layout.pictureFile} {stand.name}");
            }
        }
    }

    /// <summary>The Island test level fitted to 02_island_draw.png (Draw buttons open it until phase 2).</summary>
    public class IslandDrawFitTests
    {
        static PictureLayout Layout() => AssetDatabase.LoadAssetAtPath<PictureLayout>("Assets/Maps/Layouts/02_island_draw.asset");

        static LevelData Fitted()
        {
            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            Layout().ApplyTo(level);
            return level;
        }

        [Test]
        public void Runways_AndPads_ComeFromThePicture()
        {
            PictureLayout layout = Layout();
            LevelData level = Fitted();
            Assert.AreEqual(2, level.runways.Count);
            Runway main = level.runways[0].ToRunway();
            Assert.AreEqual(0f, main.LandingHeadingDegrees, 0.5f, "Main runway lands west to east");
            Vector2 threshold = layout.Pixel(main.Threshold);
            Assert.AreEqual(430.5f, threshold.x, 1f);
            Assert.AreEqual(462f, threshold.y, 1f);
            Runway cross = level.runways[1].ToRunway();
            Assert.AreNotEqual(0f, Mathf.DeltaAngle(cross.LandingHeadingDegrees, 0f), "The crossing runway is at an angle");
            Assert.AreEqual(GameMode.Draw, level.mode);
            Assert.AreSame(layout, level.layout);
            Assert.IsTrue(LandingRules.TryFindTouchdown(new List<Runway> { main }, new List<LandingPad>(),
                new List<Vector2> { main.Threshold + new Vector2(-2f, 0.5f), main.Threshold + main.Direction * (main.TouchdownZoneStart + main.TouchdownZoneLength / 2f) },
                LandingZoneKind.Runway, out _), "A line ending on the painted stripes lands");
            LandingPadDefinition helipad = level.landingPads.Find(p => p.kind == LandingZoneKind.Helipad);
            Assert.AreEqual(471.7f, layout.Pixel(helipad.centre).x, 1f);
        }

        [Test]
        public void Gates_AreTwoApronStandsAndTheRemoteStand_WhichCrossesTheRunway()
        {
            LevelData level = Fitted();
            Runway runway = level.runways[0].ToRunway();
            Assert.AreEqual(3, level.ground.gates.Count);
            GateDefinition remote = level.ground.gates[2];
            Assert.Less(remote.position.y, runway.Centre.y, "Remote stand is south of the runway");
            Vector2 crossing = remote.connector[remote.crossingIndex];
            Assert.IsFalse(runway.Contains(crossing), "Waits clear of the runway (on the painted hold bars)");
            Assert.Greater(remote.connector[remote.crossingIndex + 1].y, runway.Centre.y, "The next segment crosses it");
            List<Vector2> main = level.ground.mainTaxiway;
            foreach (Vector2 hold in new[] { main[0], main[main.Count - 1] })
                Assert.IsFalse(runway.Contains(hold), $"Hold point {hold} is clear of the runway");
            Assert.AreEqual(runway.Centre.y, level.ground.normalLineUp.y, 0.02f, "Line-up on the centreline");
        }

        [Test]
        public void RemoteDeparture_StopsAtTheCrossing_ThenCrossesThePaintedRunway()
        {
            LevelData level = Fitted();
            GateDefinition remote = level.ground.gates[2];
            Runway runway = level.runways[0].ToRunway();
            var g = new GroundMovement(remote.connector, remote.crossingIndex, level.ground.mainTaxiway, 0.35f, 0.6f,
                                       remote.headingDegrees, false, level.ground.normalLineUp, level.ground.reversedLineUp,
                                       level.ground.liftOffDistance);
            g.Taxi();
            for (float t = 0f; t < 10f; t += 1f / 60f) g.Step(1f / 60f, false);
            Assert.AreEqual(GroundPhase.HoldingAtCrossing, g.Phase);

            g.Cross();
            bool wasOnRunway = false;
            for (float t = 0f; t < 40f && g.Phase != GroundPhase.HoldingShort; t += 1f / 60f)
            {
                g.Step(1f / 60f, false);
                wasOnRunway |= runway.Contains(g.Position);
            }
            Assert.IsTrue(wasOnRunway, "It crossed the runway");
            Assert.AreEqual(GroundPhase.HoldingShort, g.Phase, "Then taxied on to the hold line");

            g.TakeOff(runway);
            for (float t = 0f; t < 30f && g.Phase != GroundPhase.Airborne; t += 1f / 60f)
                g.Step(1f / 60f, false);
            Assert.AreEqual(GroundPhase.Airborne, g.Phase);
        }

        [Test]
        public void CarrierLane_IsInTheOpenSeaBelowTheIsland()
        {
            PictureLayout layout = Layout();
            LevelData level = Fitted();
            Assert.IsTrue(level.carrier.hasCarrier);
            float top = layout.Pixel(new Vector2(0f, level.carrier.laneY + level.carrier.shipWidth / 2f)).y;
            float bottom = layout.Pixel(new Vector2(0f, level.carrier.laneY - level.carrier.shipWidth / 2f)).y;
            Assert.Greater(top, 730f, "Ship's top edge is below the shore");
            Assert.Less(bottom, 1024f, "Inside the picture");
        }

        [Test]
        public void TheIslandLevelAsset_UsesTheDrawPicture_AndAircraftImages()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Levels/IslandPrototype.asset");
            Assert.AreSame(Layout(), level.layout, "The test level is on 02_island_draw");
            Assert.IsNotNull(level.mapArt.sprite);
            Assert.AreEqual(10f, level.mapArt.WorldRect.height, 0.01f);
            Assert.AreEqual(2, level.runways.Count);
            foreach (AircraftType kind in level.aircraftTypes)
                Assert.IsNotNull(kind.sprite, $"{kind.displayName} has its painted image");
        }
    }

    /// <summary>The map catalog (blueprint 16.2): seven maps in order with both pictures, layouts and levels per mode.</summary>
    public class MapCatalogTests
    {
        [Test]
        public void SevenMaps_InOrder_WithPicturesLayoutsAndLevelIds()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>("Assets/Maps/MapCatalog.asset");
            Assert.IsNotNull(catalog);
            string[] names = { "Island", "City", "Desert", "Mountain Valley", "Arctic", "Military Base", "Spaceport" };
            int[] xp = { 0, 1000, 2500, 4000, 5500, 7000, 8500 };
            Assert.AreEqual(names.Length, catalog.maps.Count);
            for (int i = 0; i < names.Length; i++)
            {
                MapEntry map = catalog.maps[i];
                Assert.AreEqual(names[i], map.displayName);
                Assert.AreEqual(xp[i], map.xpToUnlock);
                Assert.AreEqual(i < 5, map.gameplayReady, "Military Base and Spaceport: coming soon");
                Assert.IsNotNull(map.drawPicture, $"{map.displayName} Draw picture");
                Assert.IsNotNull(map.towerPicture, $"{map.displayName} Tower picture");
                if (i < 5)
                {
                    Assert.IsNotNull(map.drawLayout, $"{map.displayName} Draw layout");
                    Assert.IsNotNull(map.towerLayout, $"{map.displayName} Tower layout");
                    Assert.AreSame(map.drawPicture, map.drawLayout.picture);
                }
                foreach (GameMode mode in GameModes.All)
                {
                    Assert.AreEqual(5, map.Levels(mode).Count);
                    for (int n = 0; n < 5; n++)
                        Assert.AreEqual(MapInfo.LevelId(map.id, mode, n + 1), map.Levels(mode)[n].id);
                }
            }
            for (int i = 0; i < 5; i++)
                foreach (LevelEntry level in catalog.maps[i].drawLevels)
                    Assert.IsNotNull(level.data, $"{level.id}: Draw levels of maps 1 to 5 are built (phase 2)");
            Assert.IsTrue(catalog.maps[0].towerLevels.TrueForAll(l => l.data == null), "Tower comes in phase 3");
        }
    }
}

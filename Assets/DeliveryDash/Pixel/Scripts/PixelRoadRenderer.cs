using System.Collections.Generic;
using UnityEngine;

namespace DeliveryDash.Pixel
{
    // Draws the falling road in screen perspective while the simulation stays in route metres.
    // One mesh and one material keep moving paving inexpensive in the Web build.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PixelRoadRenderer : MonoBehaviour
    {
        private const float ViewAhead = 190f;
        private const float DistantViewAhead = 600f;
        private const float SegmentMetres = 3f;
        private const float PavingRowMetres = 2.1f;
        private const float PixelWidth = 640f;
        private const float PixelHeight = 360f;

        private static readonly Color Road = Rgb(139, 134, 138);
        private static readonly Color RoadNear = Rgb(146, 140, 140);
        private static readonly Color StoneLight = Rgb(168, 159, 154);
        private static readonly Color StoneDark = Rgb(119, 117, 123);
        private static readonly Color StoneWarm = Rgb(157, 144, 138);
        private static readonly Color StoneBlue = Rgb(126, 130, 141);
        private static readonly Color Joint = Rgb(102, 98, 107);
        private static readonly Color Shoulder = Rgb(187, 171, 152);
        private static readonly Color Curb = Rgb(224, 203, 173);
        private static readonly Color CurbShadow = Rgb(109, 102, 111);
        private static readonly Color SideGround = Rgb(161, 132, 112);
        private static readonly Color SideGroundDark = Rgb(126, 105, 100);
        private static readonly Color Wood = Rgb(130, 82, 53);
        private static readonly Color WoodLight = Rgb(184, 119, 69);
        private static readonly Color WoodDark = Rgb(81, 55, 50);
        private static readonly Color Belt = Rgb(49, 67, 71);
        private static readonly Color Slick = Rgb(77, 139, 150);
        private static readonly Color Canal = Rgb(39, 107, 132);
        private static readonly Color Yellow = Rgb(238, 170, 55);
        private static readonly Color Black = Rgb(42, 51, 60);

        [SerializeField] private PixelRunSession session;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private int sortingOrder;
        [SerializeField] private Sprite[] buildingSprites;
        [SerializeField] private Sprite[] foliageSprites;

        private readonly List<Vector3> vertices = new List<Vector3>(10000);
        private readonly List<Color> colors = new List<Color>(10000);
        private readonly List<int> triangles = new List<int>(15000);
        private Mesh roadMesh;
        private Material roadMaterial;
        private MeshRenderer meshRenderer;
        private RoutePlan plan;
        private float distance;
        private float playerWorldX;
        private float smoothedLook;
        private SpriteRenderer[] buildingPool;
        private SpriteRenderer[] foliagePool;

        public void Initialize(PixelRunSession runSession, Camera camera)
        {
            session = runSession;
            viewCamera = camera;
            EnsureResources();
            RenderRoad();
        }

        public void SetScenerySprites(Sprite[] buildings, Sprite[] foliage)
        {
            buildingSprites = buildings;
            foliageSprites = foliage;
            EnsureSceneryPool();
        }

        private void Awake() { EnsureResources(); }

        private void OnEnable() { EnsureResources(); }

        private void LateUpdate()
        {
            RenderRoad();
        }

        private void EnsureResources()
        {
            if (roadMesh == null)
            {
                roadMesh = new Mesh { name = "Moving downhill road" };
                roadMesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = roadMesh;
            }
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = sortingOrder;
            if (roadMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                roadMaterial = new Material(shader) { name = "Pixel road vertex colors" };
                roadMaterial.mainTexture = Texture2D.whiteTexture;
                meshRenderer.sharedMaterial = roadMaterial;
            }
        }

        private void OnDestroy()
        {
            if (roadMesh != null) Destroy(roadMesh);
            if (roadMaterial != null) Destroy(roadMaterial);
        }

        private void RenderRoad()
        {
            if (session == null) session = FindFirstObjectByType<PixelRunSession>();
            if (viewCamera == null) viewCamera = Camera.main;
            if (session == null || session.Plan == null || session.Cart == null || viewCamera == null) return;
            EnsureResources();
            plan = session.Plan;
            distance = session.Cart.DistanceMeters;
            playerWorldX = plan.CenterX(distance) + session.Cart.LateralOffset;
            float look = (plan.CenterX(distance + 40f) - plan.CenterX(distance + 8f)) / 32f;
            smoothedLook = Mathf.Lerp(smoothedLook, look, 1f - Mathf.Exp(-5f * Time.deltaTime));
            vertices.Clear();
            colors.Clear();
            triangles.Clear();

            // Far polygons first. Each section is a real step of the route that comes toward the cart.
            for (float back = DistantViewAhead; back > ViewAhead; back -= 12f)
            {
                float d0 = distance + Mathf.Max(ViewAhead, back - 12f);
                float d1 = distance + back;
                DrawSection(d0, d1);
            }
            for (float back = ViewAhead; back > 0f; back -= SegmentMetres)
            {
                float d0 = distance + Mathf.Max(0f, back - SegmentMetres);
                float d1 = distance + back;
                DrawSection(d0, d1);
            }
            DrawPaving();
            DrawRouteProps();

            roadMesh.Clear();
            roadMesh.SetVertices(vertices);
            roadMesh.SetColors(colors);
            roadMesh.SetTriangles(triangles, 0, true);
            roadMesh.RecalculateBounds();
            UpdateScenery();
        }

        private void DrawSection(float near, float far)
        {
            float middle = (near + far) * 0.5f;
            RouteBranch branch = plan.BranchAt(middle);
            float safeHalf = 6.25f;
            Ribbon(near, far, -24f, -24f, -safeHalf - 0.45f, -safeHalf - 0.45f, SideGround);
            Ribbon(near, far, safeHalf + 0.45f, safeHalf + 0.45f, 24f, 24f, SideGround);
            Ribbon(near, far, -safeHalf - 2.1f, -safeHalf - 2.1f,
                -safeHalf - 0.45f, -safeHalf - 0.45f, Shoulder);
            Ribbon(near, far, safeHalf + 0.45f, safeHalf + 0.45f,
                safeHalf + 2.1f, safeHalf + 2.1f, Shoulder);
            if (branch != null && branch.SplitAmount(middle) > 0.2f)
            {
                float nearOuter = branch.SideOffset * branch.SplitAmount(near) + branch.CorridorWidth * 0.5f + 1.8f;
                float farOuter = branch.SideOffset * branch.SplitAmount(far) + branch.CorridorWidth * 0.5f + 1.8f;
                if (branch.RiskySide < 0)
                    Ribbon(near, far, -nearOuter, -farOuter, -safeHalf - 0.35f,
                        -safeHalf - 0.35f, Canal);
                else
                    Ribbon(near, far, safeHalf + 0.35f, safeHalf + 0.35f,
                        nearOuter, farOuter, Canal);
                DrawLane(near, far, 0f, 0f, 12.5f, false);
                DrawLane(near, far, branch.RiskyCenterOffset(near), branch.RiskyCenterOffset(far),
                    branch.CorridorWidth, true);
            }
            else
            {
                DrawLane(near, far, 0f, 0f, 12.5f, false);
            }
        }

        private void DrawLane(float near, float far, float nearOffset, float farOffset, float width, bool wood)
        {
            float half = width * 0.5f;
            float edge = half + 0.50f;
            float rim = half + 0.22f;
            Color baseColor = wood ? Wood : Color.Lerp(RoadNear, Road, Mathf.Clamp01((near - distance) / ViewAhead));
            Ribbon(near, far, nearOffset - edge, farOffset - edge,
                nearOffset + edge, farOffset + edge, wood ? SideGroundDark : CurbShadow);
            Ribbon(near, far, nearOffset - rim, farOffset - rim,
                nearOffset + rim, farOffset + rim, wood ? WoodDark : Shoulder);
            Ribbon(near, far, nearOffset - half, farOffset - half,
                nearOffset + half, farOffset + half, baseColor);
            // Bright raised stone edges give the road a readable physical boundary.
            Ribbon(near, far, nearOffset - half - 0.16f, farOffset - half - 0.16f,
                nearOffset - half + 0.06f, farOffset - half + 0.06f, Curb);
            Ribbon(near, far, nearOffset + half - 0.06f, farOffset + half - 0.06f,
                nearOffset + half + 0.16f, farOffset + half + 0.16f, Curb);
            if (wood)
            {
                Ribbon(near, far, nearOffset - half + 0.2f, farOffset - half + 0.2f,
                    nearOffset - half + 0.42f, farOffset - half + 0.42f, WoodLight);
                Ribbon(near, far, nearOffset + half - 0.42f, farOffset + half - 0.42f,
                    nearOffset + half - 0.2f, farOffset + half - 0.2f, WoodLight);
            }
        }

        private void DrawPaving()
        {
            int first = Mathf.FloorToInt(distance / PavingRowMetres);
            int last = Mathf.CeilToInt((distance + ViewAhead) / PavingRowMetres);
            for (int row = last; row >= first; row--)
            {
                float d0 = row * PavingRowMetres;
                float d1 = d0 + PavingRowMetres - 0.10f;
                if (d1 < distance || d0 > distance + ViewAhead) continue;
                RouteBranch branch = plan.BranchAt((d0 + d1) * 0.5f);
                if (branch != null && branch.SplitAmount((d0 + d1) * 0.5f) > 0.2f)
                {
                    DrawPavingLane(d0, d1, row, 0f, 0f, 12.5f, false);
                    DrawPavingLane(d0, d1, row, branch.RiskyCenterOffset(d0), branch.RiskyCenterOffset(d1),
                        branch.CorridorWidth, true);
                }
                else DrawPavingLane(d0, d1, row, 0f, 0f, 12.5f, false);
                DrawSidewalkPaving(d0, d1, row, branch);
            }
        }

        private void DrawSidewalkPaving(float near, float far, int row, RouteBranch branch)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (branch != null && branch.RiskySide == side &&
                    branch.SplitAmount((near + far) * 0.5f) > 0.3f) continue;
                for (int col = 0; col < 3; col++)
                {
                    float lo = 6.74f + col * 0.45f;
                    float hi = lo + 0.41f;
                    float l = side < 0 ? -hi : lo;
                    float r = side < 0 ? -lo : hi;
                    int value = Hash(row, col + side * 17);
                    Ribbon(near + 0.08f, far - 0.08f, l, l, r, r,
                        value % 3 == 0 ? Curb : value % 3 == 1 ? Shoulder : StoneWarm);
                }
            }
        }

        private void DrawPavingLane(float near, float far, int row, float nearOffset,
            float farOffset, float width, bool wood)
        {
            int count = wood ? 7 : (near - distance < 55f ? 24 : near - distance < 110f ? 16 : 9);
            float inset = wood ? 0.12f : 0.25f;
            float usable = width - inset * 2f;
            float tileWidth = usable / count;
            float stagger = row % 2 == 0 ? 0f : tileWidth * 0.40f;
            for (int col = 0; col < count;)
            {
                int span = wood ? 1 : 1 + Hash(row, col + 113) % 2;
                float l = -usable * 0.5f + col * tileWidth + inset + stagger;
                float r = l + tileWidth * span - (wood ? 0.08f : 0.05f);
                if (r > width * 0.5f - inset) r = width * 0.5f - inset;
                col += span;
                if (r <= l) continue;
                int value = Hash(row, col);
                Color color = wood ? (value % 4 == 0 ? WoodLight : WoodDark) :
                    (value % 5 == 0 ? StoneLight : value % 5 == 1 ? StoneDark :
                     value % 5 == 2 ? StoneWarm : value % 5 == 3 ? StoneBlue : RoadNear);
                float tileNear = near + (value % 3) * 0.12f;
                float tileFar = far - ((value / 3) % 3) * 0.14f;
                Ribbon(tileNear, tileFar, nearOffset + l, farOffset + l,
                    nearOffset + r, farOffset + r, color);
            }
        }

        private void DrawRouteProps()
        {
            for (int i = 0; i < plan.Beats.Count; i++)
            {
                RouteBeat beat = plan.Beats[i];
                if (beat.End < distance || beat.Start > distance + ViewAhead) continue;
                switch (beat.Kind)
                {
                    case RouteBeatKind.Bump: DrawBump(beat); break;
                    case RouteBeatKind.Ramp: DrawRamp(beat); break;
                    case RouteBeatKind.Conveyor: DrawConveyor(beat); break;
                    case RouteBeatKind.Slick: DrawSlick(beat); break;
                    case RouteBeatKind.Scrape: DrawScrape(beat); break;
                    case RouteBeatKind.Finish: DrawFinish(beat); break;
                }
            }
        }

        private void DrawBump(RouteBeat beat)
        {
            float half = beat.Width * 0.5f;
            float roadHalf = plan.Width(beat.Start) * 0.5f;
            // Side flags advertise the bump while the courier still overlaps the road centre.
            Ribbon(beat.Start - 20f, beat.Start - 13f, -roadHalf + 0.2f, -roadHalf + 0.2f,
                -roadHalf + 0.9f, -roadHalf + 0.9f, Yellow);
            Ribbon(beat.Start - 20f, beat.Start - 13f, roadHalf - 0.9f, roadHalf - 0.9f,
                roadHalf - 0.2f, roadHalf - 0.2f, Yellow);
            RaisedStrip(beat.Start, beat.End, beat.Offset - half, beat.Offset + half,
                3f, 5f, Black, StoneDark);
            for (int i = 0; i < 7; i += 2)
            {
                float l = beat.Offset - half + beat.Width * i / 7f;
                float r = beat.Offset - half + beat.Width * (i + 1) / 7f;
                RaisedStrip(beat.Start + 0.4f, beat.End - 0.4f, l, r,
                    3f, 5f, Yellow, StoneDark);
            }
        }

        private void DrawRamp(RouteBeat beat)
        {
            float half = beat.Width * 0.5f;
            RaisedStrip(beat.Start, beat.End, beat.Offset - half, beat.Offset + half,
                0f, 8f, WoodLight, WoodDark);
            RaisedStrip(beat.Start + 1f, beat.End - 1f, beat.Offset - half + 0.25f,
                beat.Offset + half - 0.25f, 0f, 8f, WoodLight, WoodDark);
            for (float d = beat.Start + 3f; d < beat.End - 1f; d += 4.5f)
                RaisedStrip(d, d + 0.55f, beat.Offset - half, beat.Offset + half,
                    0f, 8f * ((d - beat.Start) / (beat.End - beat.Start)), WoodDark, WoodDark);
        }

        private void DrawConveyor(RouteBeat beat)
        {
            float half = beat.Width * 0.5f;
            Ribbon(beat.Start, beat.End, beat.Offset - half, beat.Offset - half,
                beat.Offset + half, beat.Offset + half, Belt);
            Ribbon(beat.Start, beat.End, beat.Offset - half, beat.Offset - half,
                beat.Offset - half + 0.18f, beat.Offset - half + 0.18f, WoodLight);
            Ribbon(beat.Start, beat.End, beat.Offset + half - 0.18f, beat.Offset + half - 0.18f,
                beat.Offset + half, beat.Offset + half, WoodLight);
            for (float d = beat.Start + 2f; d < beat.End; d += 4f)
                Ribbon(d, d + 0.7f, beat.Offset - half + 0.2f, beat.Offset - half + 0.2f,
                    beat.Offset + half - 0.2f, beat.Offset + half - 0.2f, StoneLight);
        }

        private void DrawSlick(RouteBeat beat)
        {
            float half = beat.Width * 0.5f;
            Ribbon(beat.Start, beat.End, beat.Offset - half, beat.Offset - half,
                beat.Offset + half, beat.Offset + half, Slick);
            for (float d = beat.Start + 4f; d < beat.End; d += 7f)
                Ribbon(d, d + 0.5f, beat.Offset - half + 0.5f, beat.Offset - half + 0.5f,
                    beat.Offset + half - 0.5f, beat.Offset + half - 0.5f, Curb);
        }

        private void DrawScrape(RouteBeat beat)
        {
            float l = beat.Offset - beat.Width * 0.5f;
            float r = beat.Offset + beat.Width * 0.5f;
            Ribbon(beat.Start, beat.End, l, l, r, r, WoodDark);
            Ribbon(beat.Start + 1f, beat.End - 1f, l + 0.1f, l + 0.1f,
                r - 0.1f, r - 0.1f, WoodLight);
        }

        private void DrawFinish(RouteBeat beat)
        {
            float d = plan.LengthMeters - 10f;
            if (d < distance || d > distance + ViewAhead) return;
            for (int col = 0; col < 12; col++)
            {
                float l = -6f + col;
                Ribbon(d, d + 1.7f, l, l, l + 1f, l + 1f,
                    col % 2 == 0 ? Curb : Black);
                Ribbon(d + 1.7f, d + 3.4f, l, l, l + 1f, l + 1f,
                    col % 2 == 0 ? Black : Curb);
            }
        }

        private void EnsureSceneryPool()
        {
            if (buildingPool == null)
            {
                buildingPool = new SpriteRenderer[16];
                for (int i = 0; i < buildingPool.Length; i++)
                    buildingPool[i] = MakeBillboard("Roadside building " + i);
            }
            if (foliagePool == null)
            {
                foliagePool = new SpriteRenderer[18];
                for (int i = 0; i < foliagePool.Length; i++)
                    foliagePool[i] = MakeBillboard("Roadside foliage " + i);
            }
        }

        private SpriteRenderer MakeBillboard(string label)
        {
            GameObject prop = new GameObject(label);
            prop.transform.SetParent(transform, false);
            SpriteRenderer sprite = prop.AddComponent<SpriteRenderer>();
            sprite.sortingOrder = 3;
            return sprite;
        }

        private void UpdateScenery()
        {
            EnsureSceneryPool();
            UpdatePool(buildingPool, buildingSprites, 30f, 8.8f, 6.3f, 0);
            UpdatePool(foliagePool, foliageSprites, 24f, 8.0f, 1.9f, 179);
        }

        private void UpdatePool(SpriteRenderer[] pool, Sprite[] sprites, float spacing,
            float lateral, float nearWidth, int salt)
        {
            bool hasSprites = sprites != null && sprites.Length > 0;
            int firstStation = Mathf.FloorToInt(distance / spacing) - 1;
            for (int i = 0; i < pool.Length; i++)
            {
                SpriteRenderer billboard = pool[i];
                if (!hasSprites) { billboard.enabled = false; continue; }
                int stationIndex = firstStation + i / 2;
                float station = stationIndex * spacing + (Hash(stationIndex, salt) % 7 - 3) * 0.6f;
                float ahead = station - distance;
                if (ahead < -spacing || ahead > ViewAhead || station > plan.LengthMeters - 25f)
                {
                    billboard.enabled = false;
                    continue;
                }
                int side = i % 2 == 0 ? -1 : 1;
                RouteBranch branch = plan.BranchAt(station);
                if (branch != null && branch.RiskySide == side && branch.SplitAmount(station) > 0.35f)
                {
                    billboard.enabled = false;
                    continue;
                }
                int selection = Hash(stationIndex, salt + i) % sprites.Length;
                Sprite sprite = sprites[selection];
                if (sprite == null) { billboard.enabled = false; continue; }
                billboard.enabled = true;
                billboard.sprite = sprite;
                float scale = 1f / (1f + ahead / 26f);
                float width = nearWidth * scale * (0.88f + (Hash(stationIndex, salt + 19) % 25) * 0.01f);
                float spriteScale = width / Mathf.Max(0.01f, sprite.bounds.size.x);
                billboard.transform.localScale = new Vector3(spriteScale, spriteScale, 1f);
                float sideDistance = lateral + (Hash(stationIndex, salt + side) % 4) * 0.55f;
                Vector3 ground = transform.TransformPoint(Project(station, side * sideDistance));
                if (ahead < 0f)
                {
                    ground.x += side * -ahead * 0.18f;
                    ground.y += ahead * 0.10f;
                }
                billboard.transform.position = new Vector3(ground.x,
                    ground.y - sprite.bounds.min.y * spriteScale, transform.position.z + 0.05f);
                float nearAlpha = Mathf.Clamp01((ahead + spacing) / spacing);
                float farAlpha = Mathf.Clamp01((ViewAhead - ahead) / 24f);
                billboard.color = new Color(1f, 1f, 1f, nearAlpha * farAlpha);
                billboard.sortingOrder = 3 + Mathf.Clamp(Mathf.RoundToInt((1f - ahead / ViewAhead) * 12f), 0, 12);
            }
        }

        private void Ribbon(float near, float far, float nearLeft, float farLeft,
            float nearRight, float farRight, Color color)
        {
            Vector3 a = Project(near, nearLeft);
            Vector3 b = Project(far, farLeft);
            Vector3 c = Project(far, farRight);
            Vector3 d = Project(near, nearRight);
            Quad(a, b, c, d, color);
        }

        private void RaisedStrip(float near, float far, float left, float right,
            float nearHeight, float farHeight, Color topColor, Color faceColor)
        {
            Vector3 a = Project(near, left);
            Vector3 b = Project(far, left);
            Vector3 c = Project(far, right);
            Vector3 d = Project(near, right);
            Vector3 upA = Raise(a, nearHeight);
            Vector3 upB = Raise(b, farHeight);
            Vector3 upC = Raise(c, farHeight);
            Vector3 upD = Raise(d, nearHeight);
            Quad(a, d, upD, upA, faceColor);
            Quad(upA, upB, upC, upD, topColor);
        }

        private Vector3 Raise(Vector3 point, float pixelCount)
        {
            return point + transform.InverseTransformDirection(Vector3.up) *
                (viewCamera.orthographicSize * 2f * pixelCount / PixelHeight);
        }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            colors.Add(color); colors.Add(color); colors.Add(color); colors.Add(color);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
        }

        private Vector3 Project(float station, float lateral)
        {
            float ahead = Mathf.Max(0f, station - distance);
            float scale = 1f / (1f + ahead / 26f);
            float centerDelta = plan.CenterX(Mathf.Min(station, plan.LengthMeters)) - playerWorldX;
            float u = 0.5f + 0.18f * (1f - scale) - smoothedLook * 0.12f * (1f - scale) +
                Mathf.Clamp(centerDelta * 0.036f * Mathf.Sqrt(scale), -0.35f, 0.35f) +
                lateral * 0.065f * scale;
            float v = 0.72f - 0.78f * scale;
            u = Mathf.Round(u * PixelWidth) / PixelWidth;
            v = Mathf.Round(v * PixelHeight) / PixelHeight;
            Vector3 point = new Vector3((u - 0.5f) * 16f, (v - 0.5f) * 9f, transform.position.z);
            return transform.InverseTransformPoint(point);
        }

        private static int Hash(int row, int column)
        {
            unchecked
            {
                uint value = (uint)(row * 73856093 ^ column * 19349663 ^ 0x5a17);
                value ^= value >> 13;
                value *= 1274126177u;
                return (int)(value & 0x7fffffff);
            }
        }

        private static Color Rgb(byte red, byte green, byte blue)
        {
            return new Color32(red, green, blue, 255);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}

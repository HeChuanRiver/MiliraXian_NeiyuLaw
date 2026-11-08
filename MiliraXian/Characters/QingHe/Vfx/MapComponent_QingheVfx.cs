using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Vfx
{
    public interface IQingheVfxRenderer
    {
        bool Active { get; }

        void Update(Map map);

        void Draw(Map map);

        void Dispose();
    }

    public sealed class MapComponent_QingheVfx : MapComponent
    {
        private readonly List<IQingheVfxRenderer> renderers = new();

        public MapComponent_QingheVfx(Map map) : base(map)
        {
        }

        public static void Register(Map map, IQingheVfxRenderer renderer)
        {
            map.GetComponent<MapComponent_QingheVfx>().renderers.Add(renderer);
        }

        public static T Find<T>(Map map, System.Predicate<T> match) where T : class, IQingheVfxRenderer
        {
            foreach (IQingheVfxRenderer r in map.GetComponent<MapComponent_QingheVfx>().renderers)
            {
                if (r is T t && match(t))
                {
                    return t;
                }
            }
            return null;
        }

        public override void MapComponentUpdate()
        {
            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                IQingheVfxRenderer r = renderers[i];
                if (!r.Active)
                {
                    r.Dispose();
                    renderers.RemoveAt(i);
                    continue;
                }
                r.Update(map);
            }
        }

        public override void MapComponentDraw()
        {
            if (Verse.Find.CurrentMap != map)
            {
                return;
            }
            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                renderers[i].Draw(map);
            }
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            foreach (IQingheVfxRenderer r in renderers)
            {
                r.Dispose();
            }
            renderers.Clear();
        }

        private static readonly List<Vector3> Verts = new();
        private static readonly List<Vector2> Uvs = new();
        private static readonly List<Color> Colors = new();
        private static readonly List<int> Tris = new();

        internal static void ClearBuffers()
        {
            Verts.Clear();
            Uvs.Clear();
            Colors.Clear();
            Tris.Clear();
        }

        internal static bool BuffersEmpty => Verts.Count == 0;

        internal static void Upload(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(Verts);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();
        }

        internal static Mesh NewMesh(string name)
        {
            Mesh mesh = new() { name = name };
            mesh.MarkDynamic();
            return mesh;
        }

        internal static void AddVertex(Vector3 position, Vector2 uv, Color color)
        {
            Verts.Add(position);
            Uvs.Add(uv);
            Colors.Add(color);
        }

        internal static void AddTriangle(int a, int b, int c)
        {
            Tris.Add(a);
            Tris.Add(b);
            Tris.Add(c);
        }

        /// <summary>The texture is sampled down its middle column, so a soft round spot gives a soft-edged line.</summary>
        internal static void AddLine(Vector3 a, Vector3 b, float width, Color ca, Color cb)
        {
            Vector3 d = b - a;
            d.y = 0f;
            Vector3 s = new Vector3(-d.z, 0f, d.x).normalized * (width * 0.5f);
            int n = Verts.Count;
            AddVertex(a - s, new Vector2(0.5f, 0f), ca);
            AddVertex(a + s, new Vector2(0.5f, 1f), ca);
            AddVertex(b - s, new Vector2(0.5f, 0f), cb);
            AddVertex(b + s, new Vector2(0.5f, 1f), cb);
            AddTriangle(n, n + 2, n + 1);
            AddTriangle(n + 1, n + 2, n + 3);
        }

        internal static void AddQuad(Vector3 c, float size, Color col)
        {
            float h = size * 0.5f;
            int n = Verts.Count;
            AddVertex(new Vector3(c.x - h, c.y, c.z - h), new Vector2(0f, 0f), col);
            AddVertex(new Vector3(c.x - h, c.y, c.z + h), new Vector2(0f, 1f), col);
            AddVertex(new Vector3(c.x + h, c.y, c.z + h), new Vector2(1f, 1f), col);
            AddVertex(new Vector3(c.x + h, c.y, c.z - h), new Vector2(1f, 0f), col);
            AddTriangle(n, n + 1, n + 2);
            AddTriangle(n, n + 2, n + 3);
        }
    }
}

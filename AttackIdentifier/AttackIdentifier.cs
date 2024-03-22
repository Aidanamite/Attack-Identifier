using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using HarmonyLib;
using RaftModLoader;
using HMLLibrary;
using Object = UnityEngine.Object;


namespace AttackIdentifier
{
    public class Main : Mod
    {
        static List<HoverText> pointers;
        public static Material material;
        public void Start()
        {
            pointers = new List<HoverText>();
            Log("Mod has been loaded!");
        }

        public void Update()
        {
            for (int i = pointers.Count - 1; i >= 0; i--)
            {
                pointers[i].UpdateFacing();
                if (pointers[i].gameObject == null)
                    pointers.RemoveAt(i);
            }
        }

        public void OnModUnload()
        {
            for (int i = pointers.Count - 1; i >= 0; i--)
            {
                pointers[i].Destroy();
            }
            pointers.Clear();
            Log("Mod has been unloaded!");
        }

        [ConsoleCommand(name: "findTargets", docs: "Shows floating arrows above all blocks a shark can attack for a short time")]
        public static string MyCommand(string[] args)
        {
            for (int i = pointers.Count - 1; i >= 0; i--)
            {
                pointers[i].Destroy();
            }
            pointers.Clear();
            RaftBounds bounds = FindObjectOfType<RaftBounds>();
            if (bounds == null)
                return "Must be used in world";
            List<Block> blocks = new List<Block>();
            List<AngleRange> ranges = new List<AngleRange>();
            foreach (Block block in bounds.walkableBlocks)
                if (block is Block_Foundation)
                {
                    blocks.Add(block);
                    ranges.Add(new AngleRange());
                }
            for (int i = 0;i < blocks.Count;i++)
                for (int j = 0; j < blocks.Count; j++)
                {
                    float dist = blocks[i].transform.position.DistanceXZ(blocks[j].transform.position) / 2;
                    if (j != i && dist < 5)
                    {
                        double vary = Math.Acos(dist / 5);
                        double main = Math.Acos(((double)blocks[i].transform.position.x - blocks[j].transform.position.x) / (dist * 2)) * (blocks[i].transform.position.z < blocks[j].transform.position.z ? -1 : 1);
                        ranges[i] -= new AngleRange(main - vary, main + vary);
                        ranges[j] -= new AngleRange(main - vary + Math.PI, main + vary + Math.PI);
                    }
                }
            List<Block> tmp = new List<Block>();
            material = FindObjectOfType<GameManager>().ghostMaterialRed;
            for (int i = 0; i < blocks.Count; i++)
                if (!ranges[i].Empty && !blocks[i].Reinforced)
                {
                    tmp.Add(blocks[i]);
                    int value = (int)Math.Round(ranges[i].totalSize / Math.PI / 2 * 100);
                    pointers.Add(new HoverText(blocks[i].transform, value == 0 ? "<1%" : (value + "%") ,blocks[i].occupyingComponent));
                }
            return "Found " + tmp.Count + " blocks";
        }
    }


    public class AngleRange
    {
        List<Range> ranges = new List<Range>() { new Range(0, Angle.max) };
        public AngleRange(double Min, double Max)
        {
            ranges = new List<Range>() { new Range(Min, Max) };
        }
        public AngleRange() { }
        AngleRange(List<Range> Ranges)
        {
            ranges = Ranges;
        }

        public double totalSize
        {
            get
            {
                double tmp = 0;
                foreach (Range range in ranges)
                    tmp += range.Size;
                return tmp;
            }
        }

        public bool Empty
        {
            get
            {
                return ranges.Count == 0;
            }
        }

        public static AngleRange operator -(AngleRange a, AngleRange b)
        {
            List<Range> tmp = new List<Range>();
            foreach (Range range in b.ranges)
                tmp.AddRange(a - range);
            return new AngleRange(tmp);
        }

        public static List<Range> operator -(AngleRange a, Range b)
        {
            List<Range> tmp = new List<Range>();
            foreach (Range range in a.ranges)
                tmp.AddRange(range - b);
            return tmp;
        }

        public struct Angle
        {
            public const double max = 2 * Math.PI;
            double value;
            public Angle(double angle)
            {
                value = angle % max + (angle < 0 ? max : 0);
                if (value == 0 && angle > 0)
                    value = max;
            }

            public static implicit operator double(Angle a) => a.value;
            public static implicit operator Angle(double a) => new Angle(a);
        }
        public struct Range
        {
            Angle min;
            public Angle Size;

            public Range(double Min, double Max)
            {
                min = Min;
                Size = Max - min;
            }
            public Angle Min
            {
                get { return min; }
                set
                {
                    Size += min - value;
                    min = value;
                }
            }
            public Angle Max
            {
                get { return min + Size; }
                set
                {
                    Size = value - min;
                }
            }
            public double dMax
            {
                get { return min + Size; }
            }
            public static List<Range> operator -(Range a, Range b)
            {
                List<Range> tmp = new List<Range>();
                if (a.Size == Angle.max)
                {
                    tmp.Add(new Range(b.Max, b.min));
                    return tmp;
                }
                if (b.Size == Angle.max)
                    return tmp;
                bool flag1 = a.Contains(b.min);
                bool flag2 = a.Contains(b.Max);
                bool flag3 = b.Contains(a.min);
                bool flag4 = b.Contains(a.Max);
                if (flag3 && flag4 && flag1 == flag2)
                {
                    if (flag1)
                        tmp.Add(new Range(b.Max, b.min));
                    return tmp;
                }
                if (flag3)
                    tmp.Add(new Range(b.Max, a.Max));
                else if (flag1)
                {
                    tmp.Add(new Range(a.min, b.min));
                    if (flag2)
                        tmp.Add(new Range(b.Max, a.Max));
                }
                else
                    tmp.Add(a);
                return tmp;
            }

            public bool Contains(Angle value)
            {
                return dMax > Angle.max ? value > min || value < Max : value > min && value < dMax;
            }
        }
    }

    public class HoverText
    {
        Canvas canvas;
        public GameObject gameObject;
        float life;
        OccupyingComponent parentRenderer;
        Dictionary<Material, Color> original;

        public HoverText(Transform parent, string DisplayText, OccupyingComponent renderer)
        {
            original = new Dictionary<Material, Color>();
            parentRenderer = renderer;
            foreach (Renderer r in parentRenderer.renderers)
                foreach (Material m in r.materials)
                {
                    original.Add(m, m.GetColor("_BuildingEmission"));
                    m.SetColor("_BuildingEmission", SingletonGeneric<GameManager>.Singleton.shaderRed);
                }
            gameObject = new GameObject();
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = Vector3.zero;
            RectTransform rect = gameObject.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1, 1f);
            canvas = gameObject.AddComponent<Canvas>();
            canvas.transform.SetParent(gameObject.transform, false);
            canvas.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            RectTransform trans = canvas.GetComponent<RectTransform>();
            trans.sizeDelta = new Vector2(1, 1f);
            Text text = CreateText(gameObject.transform, 0, 0, DisplayText, 30, ComponentManager<CanvasHelper>.Value.dropText.color, 300, 45, ComponentManager<CanvasHelper>.Value.dropText.font).GetComponent<Text>();
            text.rectTransform.sizeDelta = new Vector2(text.preferredWidth, text.preferredHeight);
            CopyTextShadow(text.gameObject, ComponentManager<CanvasHelper>.Value.dropText.gameObject);
        }

        public void UpdateFacing()
        {
            if (life > 10)
            {
                life = 0;
                Destroy();
                gameObject = null;
            }
            if (gameObject != null)
            {
                life += Time.deltaTime;
                gameObject.transform.localPosition = new Vector3(0, 0.5f + (float)Math.Sin(life * 3) / 5, 0);
                Vector3 dirVec = Helper.MainCamera.transform.position - canvas.transform.position;
                float angle = Mathf.Acos(dirVec.z / dirVec.XZOnly().magnitude) / (float)Math.PI * 180f + 180;
                gameObject.transform.rotation = Quaternion.Euler(0, dirVec.x < 0 ? -angle : angle, 0);
            }
        }

        public static GameObject CreateText(Transform canvas_transform, float x, float y, string text_to_print, int font_size, Color text_color, float width, float height, Font font, string name = "Text")
        {
            GameObject UItextGO = new GameObject("Text");
            UItextGO.transform.SetParent(canvas_transform, false);
            RectTransform trans = UItextGO.AddComponent<RectTransform>();
            trans.sizeDelta = new Vector2(width, height);
            trans.anchoredPosition = new Vector2(x, y);
            Text text = UItextGO.AddComponent<Text>();
            text.text = text_to_print;
            text.font = font;
            text.fontSize = font_size;
            text.color = text_color;
            text.name = name;
            Shadow shadow = UItextGO.AddComponent<Shadow>();
            shadow.effectColor = new Color();
            return UItextGO;
        }
        public static void AddTextShadow(GameObject textObject, Color shadowColor, Vector2 shadowOffset)
        {
            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = shadowColor;
            shadow.effectDistance = shadowOffset;
        }
        public static void CopyTextShadow(GameObject textObject, GameObject shadowSource)
        {
            Shadow sourcesShadow = shadowSource.GetComponent<Shadow>();
            if (sourcesShadow == null)
                sourcesShadow = shadowSource.GetComponentInChildren<Shadow>();
            AddTextShadow(textObject, sourcesShadow.effectColor, sourcesShadow.effectDistance);
        }
        public void Destroy()
        {
            foreach (KeyValuePair<Material, Color> t in original)
                t.Key.SetColor("_BuildingEmission", t.Value);
            Object.Destroy(gameObject);
        }
    }
}
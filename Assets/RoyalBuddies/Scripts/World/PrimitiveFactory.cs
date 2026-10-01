using System;
using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Équivalents de box() / cylinder() / sphere() de main.gd (Godot) : petites primitives colorées.
    /// Le fournisseur de matériaux est configurable : à l'exécution on utilise les matériaux mis en cache de
    /// <see cref="RBGameAssets"/>, dans l'éditeur (génération de scène) le builder branche un fournisseur qui crée
    /// de vrais assets .mat pour que la scène sauvegardée les référence.
    /// </summary>
    public static class PrimitiveFactory
    {
        public static Func<Color, Material> MaterialProvider;

        /// <summary>Matériau Lit uni (même fournisseur que les primitives : asset .mat dans l'éditeur, cache à l'exécution).</summary>
        public static Material ColorMaterial(Color c) => MatFor(c);

        static Material MatFor(Color c)
        {
            if (MaterialProvider != null) return MaterialProvider(c);
            var assets = RBGameAssets.Current;
            if (assets != null && assets.litMaterial != null) return assets.ColorMaterial(c);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            return m;
        }

        static GameObject Make(PrimitiveType type, string name, Transform parent, Vector3 localPos, Color c, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(col); else UnityEngine.Object.DestroyImmediate(col);
                }
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.GetComponent<MeshRenderer>().sharedMaterial = MatFor(c);
            return go;
        }

        /// <summary>Boîte de dimensions <paramref name="size"/> centrée en <paramref name="pos"/> (BoxMesh Godot).</summary>
        public static GameObject Box(Vector3 pos, Vector3 size, Color c, Transform parent, string name = "Box", bool collider = false)
        {
            var go = Make(PrimitiveType.Cube, name, parent, pos, c, collider);
            go.transform.localScale = size;
            return go;
        }

        /// <summary>Cylindre de rayon r et hauteur h (CylinderMesh Godot ; la légère conicité 1.06 est ignorée).</summary>
        public static GameObject Cylinder(Vector3 pos, float r, float h, Color c, Transform parent, string name = "Cylinder")
        {
            var go = Make(PrimitiveType.Cylinder, name, parent, pos, c, false);
            go.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
            return go;
        }

        /// <summary>Sphère de rayon r (SphereMesh Godot).</summary>
        public static GameObject Sphere(Vector3 pos, float r, Color c, Transform parent, string name = "Sphere")
        {
            var go = Make(PrimitiveType.Sphere, name, parent, pos, c, false);
            go.transform.localScale = Vector3.one * (r * 2f);
            return go;
        }
    }
}

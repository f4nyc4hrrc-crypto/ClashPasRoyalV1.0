using System.Collections;
using UnityEngine;

namespace RoyalBuddies
{
    /// <summary>
    /// Silhouettes procédurales de GARDE, TIREUR, ÉCLAIREUR et GOBELIN (pas de modèle GLB dans Godot) :
    /// cylindre + sphère + membres. Cette classe construit le rig et anime les membres comme main.gd
    /// (balancement à la marche, coup de bras à l'attaque, chute à la mort).
    /// </summary>
    public class ProceduralRig : MonoBehaviour
    {
        public Transform rig, leftLeg, rightLeg, leftArm, rightArm, weapon;
        float phase;
        Coroutine swing;

        /// <summary>Construit le rig sous <paramref name="root"/> (équivalent de la fin de spawn() dans main.gd).</summary>
        public static ProceduralRig Build(Transform root, TroopData d, Team team)
        {
            var pr = root.gameObject.AddComponent<ProceduralRig>();
            float sz = d.size;
            var rigT = new GameObject("Rig").transform; rigT.SetParent(root, false);
            pr.rig = rigT;
            Color armor = d.armorColor, skin = RB.HexColor("#e9b78c"), dark = RB.HexColor("#313847");
            Color teamColor = team == Team.Player ? RB.PlayerColor : RB.EnemyColor;

            pr.leftLeg = Pivot("LLeg", rigT, new Vector3(-sz * .35f, .78f, 0));
            pr.rightLeg = Pivot("RLeg", rigT, new Vector3(sz * .35f, .78f, 0));
            pr.leftArm = Pivot("LArm", rigT, new Vector3(-sz * .75f, 1.48f, 0));
            pr.rightArm = Pivot("RArm", rigT, new Vector3(sz * .75f, 1.48f, 0));
            pr.weapon = Pivot("Weapon", pr.rightArm, Vector3.zero);

            PrimitiveFactory.Box(new Vector3(0, -sz * .34f, 0), new Vector3(sz * .32f, .78f, sz * .34f), dark, pr.leftLeg);
            PrimitiveFactory.Box(new Vector3(0, -sz * .34f, 0), new Vector3(sz * .32f, .78f, sz * .34f), dark, pr.rightLeg);
            PrimitiveFactory.Cylinder(new Vector3(0, 1.15f, 0), sz, 1.0f, armor, rigT, "Torso");
            PrimitiveFactory.Box(new Vector3(0, -sz * .34f, 0), new Vector3(sz * .28f, .72f, sz * .3f), teamColor, pr.leftArm);
            PrimitiveFactory.Box(new Vector3(0, -sz * .34f, 0), new Vector3(sz * .28f, .72f, sz * .3f), teamColor, pr.rightArm);
            PrimitiveFactory.Sphere(new Vector3(0, 1.93f, 0), sz * .62f, skin, rigT, "Head");
            // Yeux (côté local −Z, comme dans Godot)
            PrimitiveFactory.Sphere(new Vector3(-sz * .2f, 1.99f, -sz * .55f), sz * .075f, RB.HexColor("#202530"), rigT, "EyeL");
            PrimitiveFactory.Sphere(new Vector3(sz * .2f, 1.99f, -sz * .55f), sz * .075f, RB.HexColor("#202530"), rigT, "EyeR");

            switch (d.id)
            {
                case "GARDE":
                    PrimitiveFactory.Box(new Vector3(0, -.7f, 0), new Vector3(.12f, 1.2f, .12f), RB.HexColor("#d9e0ea"), pr.weapon, "Sword");
                    PrimitiveFactory.Box(new Vector3(-sz * 2.6f, -.5f, 0), new Vector3(.15f, .8f, .72f), teamColor, pr.rightArm, "Shield");
                    break;
                case "TIREUR":
                    PrimitiveFactory.Box(new Vector3(0, -.62f, -.18f), new Vector3(.12f, 1.05f, .12f), RB.HexColor("#5b3925"), pr.weapon, "Bow");
                    PrimitiveFactory.Box(new Vector3(0, -1.0f, -.18f), new Vector3(.7f, .08f, .08f), RB.HexColor("#d7c29d"), pr.weapon, "BowString");
                    break;
                case "ÉCLAIREUR":
                    PrimitiveFactory.Box(new Vector3(0, -.68f, 0), new Vector3(.10f, 1.0f, .10f), RB.HexColor("#d7dce5"), pr.weapon, "Blade");
                    PrimitiveFactory.Box(new Vector3(0, 2.25f, 0), new Vector3(sz * 1.25f, .18f, sz * .85f), RB.HexColor("#476e42"), rigT, "Cap");
                    break;
                case "GOBELIN":
                    Color g = RB.HexColor("#72b84c");
                    PrimitiveFactory.Sphere(new Vector3(0, 1.93f, 0), sz * .70f, g, rigT, "GoblinHead");
                    var earL = PrimitiveFactory.Box(new Vector3(-sz * .72f, 1.98f, 0), new Vector3(sz * .75f, sz * .16f, sz * .34f), g, rigT, "EarL");
                    earL.transform.localRotation = Quaternion.Euler(0, 0, -12);
                    var earR = PrimitiveFactory.Box(new Vector3(sz * .72f, 1.98f, 0), new Vector3(sz * .75f, sz * .16f, sz * .34f), g, rigT, "EarR");
                    earR.transform.localRotation = Quaternion.Euler(0, 0, 12);
                    PrimitiveFactory.Sphere(new Vector3(-sz * .21f, 2.00f, -sz * .63f), sz * .085f, RB.HexColor("#f4e96b"), rigT, "GoblinEyeL");
                    PrimitiveFactory.Sphere(new Vector3(sz * .21f, 2.00f, -sz * .63f), sz * .085f, RB.HexColor("#f4e96b"), rigT, "GoblinEyeR");
                    PrimitiveFactory.Box(new Vector3(0, -.62f, -.03f), new Vector3(.12f, .95f, .12f), RB.HexColor("#d9e0ea"), pr.weapon, "Dagger");
                    pr.weapon.localRotation = Quaternion.Euler(0, 0, -18);
                    break;
            }
            return pr;
        }

        static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        /// <summary>Balancement de marche : anim += dt·vitesse·5.2 ; jambes ±.5 rad, bras ±.28 rad.</summary>
        public void Walk(float dt, float speed)
        {
            phase += dt * speed * 5.2f;
            float s = Mathf.Sin(phase);
            leftLeg.localRotation = Quaternion.Euler(s * .5f * Mathf.Rad2Deg, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-s * .5f * Mathf.Rad2Deg, 0, 0);
            leftArm.localRotation = Quaternion.Euler(-s * .28f * Mathf.Rad2Deg, 0, 0);
            rightArm.localRotation = Quaternion.Euler(s * .28f * Mathf.Rad2Deg, 0, 0);
        }

        /// <summary>Coup de bras : −95° en .08 s puis +15° en .12 s.</summary>
        public void AttackSwing()
        {
            if (swing != null) StopCoroutine(swing);
            swing = StartCoroutine(SwingRoutine());
        }

        IEnumerator SwingRoutine()
        {
            yield return RotateArm(-95f, .08f);
            yield return RotateArm(15f, .12f);
            swing = null;
        }

        IEnumerator RotateArm(float xDeg, float dur)
        {
            Quaternion from = rightArm.localRotation, to = Quaternion.Euler(xDeg, 0, 0);
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                rightArm.localRotation = Quaternion.Slerp(from, to, t / dur);
                yield return null;
            }
            rightArm.localRotation = to;
        }

        /// <summary>Chute à la mort : rig tourne à (80, 0, 20) en .18 s.</summary>
        public IEnumerator Fall()
        {
            Quaternion from = rig.localRotation, to = Quaternion.Euler(80, 0, 20);
            for (float t = 0; t < .18f; t += Time.deltaTime)
            {
                rig.localRotation = Quaternion.Slerp(from, to, t / .18f);
                yield return null;
            }
            rig.localRotation = to;
        }
    }
}

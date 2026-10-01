using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RoyalBuddies.Tests
{
    public class FjordEnvironmentTests
    {
        [UnityTest]
        public IEnumerator BattleStarts_EnvironmentTogglePreservesArena_AndTroopCrossesBridge()
        {
            yield return SceneManager.LoadSceneAsync("RB_Battle");
            yield return null; yield return null;
            var gm=GameManager.I;
            Assert.IsNotNull(gm); Assert.IsTrue(gm.BattleStarted); Assert.AreEqual(6,gm.Towers.Count);
            if(gm.AI)gm.AI.enabled=false;
            var environment=GameObject.Find("VikingEnvironment"); Assert.IsNotNull(environment);
            Assert.AreEqual(0,environment.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(0,environment.GetComponentsInChildren<NavMeshObstacle>(true).Length);
            Assert.AreEqual(0,environment.GetComponentsInChildren<Light>(true).Length);
            var arena=GameObject.Find("RB_Arena");
            var transforms=arena.GetComponentsInChildren<Transform>(true);
            var matrices=transforms.Select(t=>t.localToWorldMatrix).ToArray();
            var navigation=NavMesh.CalculateTriangulation();
            Assert.Greater(navigation.vertices.Length,0);
            NavMeshHit hit;
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(-5.6f,0,0),out hit,.5f,NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(5.6f,0,0),out hit,.5f,NavMesh.AllAreas));
            Assert.IsFalse(NavMesh.SamplePosition(Vector3.zero,out hit,.5f,NavMesh.AllAreas));
            yield return PlayModeTests.Capture("fjord_battle_active");
            environment.SetActive(false);yield return null;
            for(int i=0;i<transforms.Length;i++) Assert.AreEqual(matrices[i],transforms[i].localToWorldMatrix,"Arena transform "+transforms[i].name);
            CollectionAssert.AreEqual(navigation.vertices,NavMesh.CalculateTriangulation().vertices);
            CollectionAssert.AreEqual(navigation.indices,NavMesh.CalculateTriangulation().indices);
            Assert.AreEqual(6,gm.Towers.Count);
            yield return PlayModeTests.Capture("fjord_disabled");
            environment.SetActive(true);yield return null;
            // Isolate one real combat traversal from random opponent deployments.
            if(gm.AI)gm.AI.enabled=false;
            PlayModeTests.Spawn("GEANT",new Vector3(-5.6f,0,-7),Team.Player);
            yield return null;var knight=gm.Units.Last(u=>u.Team==Team.Player);
            bool crossed=false,damaged=false;Time.timeScale=5;
            try
            {
                float elapsed=0;
                while(elapsed<40 && !damaged)
                {
                    yield return null;elapsed+=Time.deltaTime;
                    if(knight && knight.Position.z>2.5f)crossed=true;
                    damaged=gm.Towers.Any(t=>t.Team==Team.Enemy && t.Hp<t.MaxHp);
                }
            }
            finally {Time.timeScale=1;}
            Assert.IsTrue(crossed,"Troop crosses the original bridge");Assert.IsTrue(damaged,"Troop damages an enemy tower");
            yield return PlayModeTests.Capture("fjord_combat");
            var cam=Camera.main;var pos=cam.transform.position;var rot=cam.transform.rotation;
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas).ToArray();
            foreach(var canvas in canvases)canvas.enabled=false;
            cam.transform.position=new Vector3(40,48,-54);cam.transform.LookAt(new Vector3(0,0,6));
            yield return PlayModeTests.Capture("fjord_overview",1500,1200);
            cam.transform.position=pos;cam.transform.rotation=rot;
            foreach(var canvas in canvases)canvas.enabled=true;
            Object.Destroy(environment);yield return null;
            Assert.IsNotNull(arena);Assert.AreEqual(6,gm.Towers.Count);
            CollectionAssert.AreEqual(navigation.vertices,NavMesh.CalculateTriangulation().vertices);
        }
    }
}

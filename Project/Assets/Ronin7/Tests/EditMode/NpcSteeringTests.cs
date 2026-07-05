using NUnit.Framework;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the obstacle probe behind the NPC clipping fix: NpcWander/StoryNpcWander/NpcWalker
    /// move NPCs by raw transform interpolation, and <see cref="NpcSteering.PathBlocked"/> is the
    /// single check that keeps them from walking through props/walls. Uses real colliders — physics
    /// spatial queries run fine in EditMode once Physics.SyncTransforms() is called.
    /// </summary>
    public class NpcSteeringTests
    {
        private GameObject _npc;
        private GameObject _obstacle;

        [SetUp]
        public void SetUp()
        {
            _npc = new GameObject("Npc");
            _npc.transform.position = Vector3.zero;
        }

        [TearDown]
        public void TearDown()
        {
            if (_npc != null) Object.DestroyImmediate(_npc);
            if (_obstacle != null) Object.DestroyImmediate(_obstacle);
        }

        private GameObject MakeWall(Vector3 position, bool isTrigger = false)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.position = position;
            wall.transform.localScale = new Vector3(2f, 2f, 0.2f);
            wall.GetComponent<Collider>().isTrigger = isTrigger;
            Physics.SyncTransforms();
            return wall;
        }

        [Test]
        public void PathBlocked_WallBetweenNpcAndDest_ReturnsTrue()
        {
            _obstacle = MakeWall(new Vector3(0f, 1f, 2f));
            Assert.IsTrue(NpcSteering.PathBlocked(_npc.transform, new Vector3(0f, 0f, 4f)),
                "A solid wall across the leg must block the path.");
        }

        [Test]
        public void PathBlocked_NoObstacle_ReturnsFalse()
        {
            Physics.SyncTransforms();
            Assert.IsFalse(NpcSteering.PathBlocked(_npc.transform, new Vector3(0f, 0f, 4f)));
        }

        [Test]
        public void PathBlocked_TriggerVolume_IsIgnored()
        {
            _obstacle = MakeWall(new Vector3(0f, 1f, 2f), isTrigger: true);
            Assert.IsFalse(NpcSteering.PathBlocked(_npc.transform, new Vector3(0f, 0f, 4f)),
                "Talk zones / story triggers are trigger colliders and must never block an NPC.");
        }

        [Test]
        public void PathBlocked_WallBeyondDestination_ReturnsFalse()
        {
            _obstacle = MakeWall(new Vector3(0f, 1f, 6f));
            Assert.IsFalse(NpcSteering.PathBlocked(_npc.transform, new Vector3(0f, 0f, 4f)),
                "Geometry past the destination is not in the way.");
        }

        [Test]
        public void PathBlocked_LookaheadLimited_IgnoresFarWall()
        {
            _obstacle = MakeWall(new Vector3(0f, 1f, 2f));
            Assert.IsFalse(NpcSteering.PathBlocked(_npc.transform, new Vector3(0f, 0f, 4f), NpcSteering.Lookahead),
                "The per-frame guard only probes the next stretch — a wall 2m out must not stop a step now.");
            Assert.IsTrue(NpcSteering.PathBlocked(_npc.transform, new Vector3(0f, 0f, 4f), 3f),
                "The same wall must block once it is inside the probed distance.");
        }

        [Test]
        public void PathBlocked_NpcsOwnChildCollider_DoesNotBlockItself()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(_npc.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0.2f); // overlaps the probe start
            Physics.SyncTransforms();

            Assert.IsFalse(NpcSteering.PathBlocked(_npc.transform, new Vector3(0f, 0f, 4f)),
                "An NPC's own collider hierarchy must never register as an obstacle.");
        }

        [Test]
        public void PathBlocked_FlatFloor_DoesNotBlock()
        {
            _obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _obstacle.name = "Floor";
            _obstacle.transform.position = new Vector3(0f, -0.05f, 2f);
            _obstacle.transform.localScale = new Vector3(10f, 0.1f, 10f);
            Physics.SyncTransforms();

            Assert.IsFalse(NpcSteering.PathBlocked(_npc.transform, new Vector3(0f, 0f, 4f)),
                "The chest-height probe must clear a flat floor under the walk.");
        }
    }
}

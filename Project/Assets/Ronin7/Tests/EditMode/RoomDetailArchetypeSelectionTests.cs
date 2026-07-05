using System.Linq;
using NUnit.Framework;
using Ronin7.EditorTools;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Coverage for the pure hash-selection logic behind the room-detail variety pass
    /// (<see cref="XRRigBuilder.StableHash"/> / <see cref="XRRigBuilder.PickRoomDetailArchetypes"/>).
    /// Procedural room dressing must be reproducible across editor sessions and chapter rebuilds, so both
    /// functions must be process-independent and depend only on the room name — no <c>Random</c>, no
    /// <c>string.GetHashCode()</c> (which .NET randomizes per-process for security).
    /// </summary>
    public class RoomDetailArchetypeSelectionTests
    {
        // Renderer cost per archetype index, mirroring BuildStackedCratesExtra (3) / BuildCableRunExtra (2)
        // / BuildFloorGrateExtra (1) / BuildSignagePanelExtra (1) in ChapterSharedBuilders.cs.
        private static readonly int[] ArchetypeCost = { 3, 2, 1, 1 };

        [TestCase("Medbay")]
        [TestCase("Hold")]
        [TestCase("Command")]
        [TestCase("Market")]
        [TestCase("")]
        public void StableHash_IsDeterministicAcrossCalls(string roomName)
        {
            uint first = XRRigBuilder.StableHash(roomName);
            uint second = XRRigBuilder.StableHash(roomName);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void StableHash_DiffersForDifferentRoomNames()
        {
            // Not a strict requirement of a hash function, but true for this specific room-name set —
            // regressions here would silently collapse room variety.
            var names = new[] { "Medbay", "Hold", "Command", "Alley", "Market", "BrokerFront", "Auction",
                "Cells", "Vault", "Dock", "Throat", "Sink", "Mast", "KerraxHold", "MorriganSpine",
                "Cradle", "Vesting", "Proving", "CrewCommons", "IronDojoBay", "ArchiveHolds",
                "WarRoomTable", "SurgeryReactor" };
            var hashes = names.Select(XRRigBuilder.StableHash).Distinct().ToArray();
            Assert.AreEqual(names.Length, hashes.Length, "expected every room name to hash to a distinct value");
        }

        [TestCase("Medbay")]
        [TestCase("Hold")]
        [TestCase("Command")]
        [TestCase("Alley")]
        [TestCase("Market")]
        [TestCase("Vault")]
        [TestCase("MorriganSpine")]
        [TestCase("SurgeryReactor")]
        public void PickRoomDetailArchetypes_IsDeterministicAcrossCalls(string roomName)
        {
            var first = XRRigBuilder.PickRoomDetailArchetypes(roomName);
            var second = XRRigBuilder.PickRoomDetailArchetypes(roomName);
            CollectionAssert.AreEqual(first, second);
        }

        [TestCase("Medbay")]
        [TestCase("Hold")]
        [TestCase("Command")]
        [TestCase("Alley")]
        [TestCase("Market")]
        [TestCase("BrokerFront")]
        [TestCase("Auction")]
        [TestCase("Cells")]
        [TestCase("Vault")]
        [TestCase("Dock")]
        [TestCase("Throat")]
        [TestCase("Sink")]
        [TestCase("Mast")]
        [TestCase("KerraxHold")]
        [TestCase("MorriganSpine")]
        [TestCase("Cradle")]
        [TestCase("Vesting")]
        [TestCase("Proving")]
        [TestCase("CrewCommons")]
        [TestCase("IronDojoBay")]
        [TestCase("ArchiveHolds")]
        [TestCase("WarRoomTable")]
        [TestCase("SurgeryReactor")]
        public void PickRoomDetailArchetypes_ReturnsValidNonEmptyDistinctIndicesWithinBudget(string roomName)
        {
            var archetypes = XRRigBuilder.PickRoomDetailArchetypes(roomName);

            Assert.That(archetypes.Length, Is.EqualTo(1).Or.EqualTo(2));
            CollectionAssert.AllItemsAreUnique(archetypes);
            foreach (int a in archetypes)
                Assert.That(a, Is.InRange(0, 3));

            int totalCost = archetypes.Sum(a => ArchetypeCost[a]);
            Assert.That(totalCost, Is.LessThanOrEqualTo(4),
                "combined added-renderer cost must stay within the ~4-per-room budget");
        }
    }
}

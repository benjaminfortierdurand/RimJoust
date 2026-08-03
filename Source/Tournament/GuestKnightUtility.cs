using System.Collections.Generic;
using GiddyUp;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimJoust.Tournament
{
	public class GuestLoadout : IExposable
	{
		public Pawn guest;
		public Pawn tempHorse;
		public ThingWithComps storedWeapon;
		public ThingWithComps givenLance;
		public bool gaveLance;

		public void ExposeData()
		{
			Scribe_References.Look(ref guest, "guest");
			Scribe_References.Look(ref tempHorse, "tempHorse");
			Scribe_References.Look(ref storedWeapon, "storedWeapon");
			Scribe_References.Look(ref givenLance, "givenLance");
			Scribe_Values.Look(ref gaveLance, "gaveLance", false);
		}
	}

	public class KnightLoadout : IExposable
	{
		public Pawn knight;
		public ThingWithComps storedWeapon;
		public ThingWithComps givenLance;
		public bool gaveLance;

		public void ExposeData()
		{
			Scribe_References.Look(ref knight, "knight");
			Scribe_References.Look(ref storedWeapon, "storedWeapon");
			Scribe_References.Look(ref givenLance, "givenLance");
			Scribe_Values.Look(ref gaveLance, "gaveLance", false);
		}
	}

	public static class GuestKnightUtility
	{
		public const int MinJoustLevel = 6;

		public static int JoustLevelOf(Pawn p) => JoustSkill.LevelOf(p);

		public static bool IsEligibleGuest(Pawn p)
		{
			if (p == null || !p.Spawned || p.Dead || p.Downed) return false;
			if (!p.RaceProps.Humanlike) return false;
			if (p.Faction == null || p.Faction.IsPlayer) return false;
			if (p.HostileTo(Faction.OfPlayer)) return false;
			if (p.IsPrisoner || p.IsSlave) return false;
			if (p.WorkTagIsDisabled(WorkTags.Violent)) return false;
			if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < 16) return false;
			return true;
		}

		public static GuestLoadout EquipAndMount(Pawn guest, Map map)
		{
			var loadout = new GuestLoadout { guest = guest };
			if (guest == null || map == null) return loadout;

			guest.GetLord()?.RemovePawn(guest);
			if (guest.drafter == null) guest.drafter = new Pawn_DraftController(guest);
			guest.drafter.Drafted = true;

			var lanceDef = RimJoustDefOf.RimJoust_JoustingLance;
			if (lanceDef != null && guest.equipment != null)
			{
				var prev = guest.equipment.Primary;
				if (prev != null && guest.inventory != null
					&& guest.equipment.TryTransferEquipmentToContainer(prev, guest.inventory.innerContainer))
					loadout.storedWeapon = prev;

				if (guest.equipment.Primary == null)
				{
					var lance = (ThingWithComps)ThingMaker.MakeThing(lanceDef, GenStuff.DefaultStuffFor(lanceDef));
					guest.equipment.AddEquipment(lance);
					loadout.givenLance = lance;
					loadout.gaveLance = true;
				}
			}

			var horse = TryMakeTempHorse(guest, map);
			if (horse != null)
			{
				loadout.tempHorse = horse;
				guest.GoMount(horse, MountUtility.GiveJobMethod.Instant);
			}
			else
			{
				Log.Warning("[RimJoust] Aucun PawnKind 'Horse' trouvé : l'invité " + guest.LabelShort
					+ " joutera à pied (pas de désarçonnement possible).");
			}

			guest.jobs?.ClearQueuedJobs();
			var hold = JobMaker.MakeJob(JobDefOf.Wait_Combat);
			hold.expiryInterval = 5000;
			guest.jobs?.StartJob(hold, JobCondition.InterruptForced);

			if (TournamentManager.DebugGuests)
				Log.Message($"[RimJoust:guest] équipé {guest.LabelShort} : drafté={guest.Drafted}, "
					+ $"monté={guest.IsMounted()}, lord={guest.GetLord() != null}, "
					+ $"arme={guest.equipment?.Primary?.def.defName ?? "<aucune>"}, "
					+ $"cheval={(loadout.tempHorse != null ? loadout.tempHorse.LabelShort : "<aucun>")}");
			return loadout;
		}

		public static void RemountGuest(GuestLoadout loadout, Map map)
		{
			var guest = loadout?.guest;
			if (guest == null || guest.Dead || !guest.Spawned || guest.IsMounted()) return;

			if (guest.drafter == null) guest.drafter = new Pawn_DraftController(guest);
			guest.drafter.Drafted = true;

			var horse = loadout.tempHorse;
			if (horse == null || horse.Dead || !horse.Spawned)
			{
				horse = TryMakeTempHorse(guest, map);
				loadout.tempHorse = horse;
			}
			if (horse != null) guest.GoMount(horse, MountUtility.GiveJobMethod.Instant);
		}

		private static Pawn TryMakeTempHorse(Pawn guest, Map map)
		{
			var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Horse");
			if (kind == null) return null;
			var horse = PawnGenerator.GeneratePawn(kind, guest.Faction);
			var cell = CellFinder.RandomClosewalkCellNear(guest.Position, map, 2);
			GenSpawn.Spawn(horse, cell, map);

			var speedHediff = RimJoustDefOf.RimJoust_TourneyMount;
			if (speedHediff != null) horse.health?.AddHediff(speedHediff);
			return horse;
		}

		public static KnightLoadout GiveLance(Pawn knight)
		{
			var loadout = new KnightLoadout { knight = knight };
			var lanceDef = RimJoustDefOf.RimJoust_JoustingLance;
			if (knight == null || lanceDef == null || knight.equipment == null || knight.HasJoustingLance())
				return loadout;

			var prev = knight.equipment.Primary;
			if (prev != null && knight.inventory != null
				&& knight.equipment.TryTransferEquipmentToContainer(prev, knight.inventory.innerContainer))
				loadout.storedWeapon = prev;

			if (knight.equipment.Primary != null) return loadout;

			var lance = (ThingWithComps)ThingMaker.MakeThing(lanceDef, GenStuff.DefaultStuffFor(lanceDef));
			knight.equipment.AddEquipment(lance);
			loadout.givenLance = lance;
			loadout.gaveLance = true;
			return loadout;
		}

		public static KnightLoadout GiveTourneyMace(Pawn fighter)
			=> GiveLoanerWeapon(fighter, RimJoustDefOf.RimJoust_TourneyMace);

		public static KnightLoadout GiveTourneyBow(Pawn archer)
			=> GiveLoanerWeapon(archer, DefDatabase<ThingDef>.GetNamedSilentFail("Bow_Recurve"));

		private static KnightLoadout GiveLoanerWeapon(Pawn fighter, ThingDef weaponDef)
		{
			var loadout = new KnightLoadout { knight = fighter };
			if (fighter == null || weaponDef == null || fighter.equipment == null) return loadout;

			var prev = fighter.equipment.Primary;
			if (prev != null && fighter.inventory != null
				&& fighter.equipment.TryTransferEquipmentToContainer(prev, fighter.inventory.innerContainer))
				loadout.storedWeapon = prev;

			if (fighter.equipment.Primary != null) return loadout;

			var weapon = (ThingWithComps)ThingMaker.MakeThing(weaponDef, GenStuff.DefaultStuffFor(weaponDef));
			fighter.equipment.AddEquipment(weapon);
			loadout.givenLance = weapon;
			loadout.gaveLance = true;
			return loadout;
		}

		public static void EnsureLance(KnightLoadout loadout)
		{
			var knight = loadout?.knight;
			if (knight == null || knight.Dead || knight.equipment == null) return;
			if (knight.HasJoustingLance()) return;

			var prev = knight.equipment.Primary;
			if (prev != null && knight.inventory != null
				&& knight.equipment.TryTransferEquipmentToContainer(prev, knight.inventory.innerContainer)
				&& loadout.storedWeapon == null)
				loadout.storedWeapon = prev;

			if (knight.equipment.Primary != null) return;

			var given = loadout.givenLance;
			if (given != null && !given.Destroyed && knight.inventory != null
				&& knight.inventory.innerContainer.Contains(given))
			{
				knight.inventory.innerContainer.Remove(given);
				knight.equipment.AddEquipment(given);
				return;
			}

			var lanceDef = RimJoustDefOf.RimJoust_JoustingLance;
			if (lanceDef == null) return;
			var lance = (ThingWithComps)ThingMaker.MakeThing(lanceDef, GenStuff.DefaultStuffFor(lanceDef));
			knight.equipment.AddEquipment(lance);
			loadout.givenLance = lance;
			loadout.gaveLance = true;
		}

		private static void DestroyGivenLance(ThingWithComps given)
		{
			if (given == null || given.Destroyed) return;
			given.holdingOwner?.Remove(given);
			if (!given.Destroyed) given.Destroy();
		}

		public static void RestoreLance(KnightLoadout loadout)
		{
			if (loadout == null) return;
			var knight = loadout.knight;
			var modGave = loadout.gaveLance || loadout.storedWeapon != null;

			if (loadout.givenLance != null)
			{
				DestroyGivenLance(loadout.givenLance);
			}
			else if (modGave && knight != null && !knight.Dead && knight.equipment != null)
			{
				var lance = knight.equipment.Primary;
				if (lance != null && lance.def.IsJoustingLance())
					knight.equipment.DestroyEquipment(lance);
			}

			if (knight == null || knight.Dead || knight.equipment == null) return;
			if (loadout.storedWeapon != null && knight.equipment.Primary == null && knight.inventory != null
				&& knight.inventory.innerContainer.Contains(loadout.storedWeapon))
			{
				knight.inventory.innerContainer.Remove(loadout.storedWeapon);
				knight.equipment.AddEquipment(loadout.storedWeapon);
			}
		}

		public static void ReleaseGuest(GuestLoadout loadout)
		{
			var guest = loadout?.guest;
			if (guest == null) return;

			if (!guest.Dead && guest.IsMounted())
			{
				var data = ExtendedDataStorage.Singleton?.GetExtendedPawnData(guest);
				if (data != null) guest.Dismount(data.Mount, data, clearReservation: true);
			}

			if (loadout.givenLance != null)
			{
				DestroyGivenLance(loadout.givenLance);
			}
			else if (guest.equipment != null)
			{
				var modGave = loadout.gaveLance || loadout.storedWeapon != null;
				var lance = guest.equipment.Primary;
				if (modGave && lance != null && lance.def.IsJoustingLance())
					guest.equipment.DestroyEquipment(lance);
			}

			if (guest.equipment != null && loadout.storedWeapon != null && guest.equipment.Primary == null
				&& guest.inventory != null && guest.inventory.innerContainer.Contains(loadout.storedWeapon))
			{
				guest.inventory.innerContainer.Remove(loadout.storedWeapon);
				guest.equipment.AddEquipment(loadout.storedWeapon);
			}

			var horse = loadout.tempHorse;
			if (horse != null && horse.Spawned && !horse.Destroyed)
				horse.Destroy();

			if (guest.drafter != null) { guest.drafter.Drafted = false; guest.drafter = null; }
			if (!guest.Dead && guest.Spawned && guest.Map != null)
			{
				guest.jobs?.EndCurrentJob(JobCondition.InterruptForced);
				if (guest.GetLord() == null && guest.Faction != null)
					LordMaker.MakeNewLord(guest.Faction,
						new LordJob_ExitMapBest(LocomotionUrgency.Walk), guest.Map, new List<Pawn> { guest });
			}
		}
	}
}

using System.Collections.Generic;
using GiddyUp;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimJoust.Jobs
{
	public class JobDriver_JoustPass : JobDriver
	{
		private IntVec3 RawSpotA => job.targetA.Cell;
		private IntVec3 RawSpotB => job.targetB.Cell;
		private Pawn Opponent => job.targetC.Thing as Pawn;

		private const int MaxStuckTicks = 500;
		private const int CornerPauseTicks = 45;
		private const int StartCountdownTicks = 180;
		private const float MaxStrikeDistance = 7.0f;
		private const int MaxSquireWaitTicks = 1800;

		private int _step;
		private bool _atCorner;
		private int _cornerWait;
		private int _startReadyTicks;
		private bool _initialized;
		private bool _struckThisPass;
		private int _laneSign;
		private float _prevDist = -1f;
		private int _stuckTicks;
		private bool _started;
		private int _lastCdSecond = -1;

		private JobDriver_JoustPass OpponentDriver => Opponent?.jobs?.curDriver as JobDriver_JoustPass;

		public bool IsAtCorner => _atCorner;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref _step, "step");
			Scribe_Values.Look(ref _atCorner, "atCorner");
			Scribe_Values.Look(ref _cornerWait, "cornerWait");
			Scribe_Values.Look(ref _startReadyTicks, "startReadyTicks");
			Scribe_Values.Look(ref _initialized, "initialized");
			Scribe_Values.Look(ref _struckThisPass, "struckThisPass");
			Scribe_Values.Look(ref _laneSign, "laneSign");
			Scribe_Values.Look(ref _prevDist, "prevDist", -1f);
			Scribe_Values.Look(ref _stuckTicks, "stuckTicks");
			Scribe_Values.Look(ref _started, "started");
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOn(() => Opponent == null || Opponent.Dead);
			this.FailOn(() => Opponent == null || !Opponent.IsMounted());
			this.FailOn(() => !pawn.IsMounted());
			this.FailOn(() => !RawSpotA.IsValid || !RawSpotB.IsValid);

			AddFinishAction(condition =>
			{
				if (condition == JobCondition.InterruptForced) return;
				var tm = RimJoust.Tournament.TournamentManager.Instance;
				if (tm != null && tm.Active && tm.IsParticipant(pawn)) return;
				if (pawn?.drafter != null && !pawn.Downed)
					pawn.drafter.Drafted = false;
			});

			var loop = new Toil
			{
				defaultCompleteMode = ToilCompleteMode.Never,
			};

			loop.initAction = () =>
			{
				if (!_initialized)
				{
					_laneSign = Opponent != null && pawn.thingIDNumber > Opponent.thingIDNumber ? -1 : 1;
					_step = _laneSign > 0 ? 0 : 2;
					_initialized = true;
				}
				StartCharging();
			};

			loop.tickAction = () =>
			{
				if (RimJoust.Tournament.TournamentManager.DebugGuests
					&& Find.TickManager.TicksGame % 60 == 0)
				{
					var dist = Opponent != null ? pawn.Position.DistanceTo(Opponent.Position) : -1f;
					var perp = PerpendicularUnit();
					var rel = pawn.Position - RawSpotA;
					var perpOff = rel.x * perp.x + rel.z * perp.z;
					Log.Message($"[RimJoust:joust] {pawn.LabelShort} (joueur={pawn.Faction?.IsPlayer}) "
						+ $"monté={pawn.IsMounted()} step={_step} perpOff={perpOff} started={_started} "
						+ $"distAdv={dist:F1}");
				}

				var inPass = _step == 1 || _step == 3;
				if (inPass && !_struckThisPass && Opponent != null)
				{
					var d = pawn.Position.DistanceTo(Opponent.Position);
					if (_prevDist >= 0f && d > _prevDist && _prevDist <= MaxStrikeDistance)
					{
						StrikeOpponentInPassing();
						_struckThisPass = true;
					}
					_prevDist = d;
				}
				else
				{
					_prevDist = -1f;
				}

				var destination = CurrentDestination();
				var arrived = pawn.Position == destination
					|| pawn.Position.AdjacentTo8WayOrInside(destination)
					|| (!pawn.pather.Moving && pawn.Position.DistanceTo(destination) < 4f);

				if (arrived)
				{
					_stuckTicks = 0;
					_atCorner = true;
					_cornerWait++;

					var isLeader = Opponent == null || pawn.thingIDNumber <= Opponent.thingIDNumber;
					if (isLeader)
					{
						var od = OpponentDriver;
						var bothAtCorner = _atCorner && (od == null || od.IsAtCorner);

						if (!_started)
						{
							var tm = RimJoust.Tournament.TournamentManager.Instance;
							var squiresReady = tm == null || !tm.Active || tm.SquiresReady(pawn.Map)
								|| _cornerWait >= MaxSquireWaitTicks;

							if (bothAtCorner && squiresReady)
							{
								_startReadyTicks++;
								var ticksLeft = StartCountdownTicks - _startReadyTicks;
								tm?.ReportStartCountdown(ticksLeft, pawn.Map);

								var sec = ticksLeft > 0 ? (ticksLeft + 59) / 60 : 0;
								if (sec != _lastCdSecond)
								{
									_lastCdSecond = sec;
									if (sec > 0) JoustSounds.CountdownTick();
								}

								if (_startReadyTicks >= StartCountdownTicks)
								{
									_started = true;
									od?.MarkStarted();
									BeginPass();
									od?.BeginPass();
									JoustSounds.Horn(new TargetInfo(pawn.Position, pawn.Map));
									tm?.FlashStartGo(pawn.Map);
								}
							}
							else
							{
								_startReadyTicks = 0;
								_lastCdSecond = -1;
							}
						}
						else if (_cornerWait >= CornerPauseTicks && bothAtCorner)
						{
							BeginPass();
							od?.BeginPass();
						}
					}
				}
				else if (!pawn.pather.Moving)
				{
					_atCorner = false;
					_stuckTicks++;
					if (_stuckTicks > MaxStuckTicks)
					{
						EndJobWith(JobCondition.Incompletable);
						return;
					}
					StartCharging();
				}
				else
				{
					_atCorner = false;
					_stuckTicks = 0;
				}
			};

			yield return loop;
		}

		private void StartCharging()
		{
			pawn.pather.StartPath(CurrentDestination(), PathEndMode.OnCell);
		}

		private IntVec3 CurrentDestination()
		{
			var perp = PerpendicularUnit();
			switch (_step)
			{
				case 1: return RawSpotB + perp;
				case 2: return RawSpotB - perp;
				case 3: return RawSpotA - perp;
				default: return RawSpotA + perp;
			}
		}

		public void BeginPass()
		{
			_atCorner = false;
			_cornerWait = 0;
			_started = true;
			AdvanceStep();
			StartCharging();
		}

		public void MarkStarted() => _started = true;

		private void AdvanceStep()
		{
			_step = (_step + 1) % 4;
			_struckThisPass = false;
		}

		private IntVec3 PerpendicularUnit()
		{
			var dx = RawSpotB.x - RawSpotA.x;
			var dz = RawSpotB.z - RawSpotA.z;
			if (dx == 0 && dz == 0) return IntVec3.Zero;

			if (System.Math.Abs(dx) >= System.Math.Abs(dz))
				return new IntVec3(0, 0, 1);
			return new IntVec3(1, 0, 0);
		}

		private void StrikeOpponentInPassing()
		{
			var weapon = pawn.equipment?.Primary;
			if (weapon == null || !weapon.def.IsJoustingLance()) return;

			var lanceDamage = RimJoustMod.Settings?.lanceDamage ?? 9f;

			var crossFaction = Opponent.Faction != pawn.Faction;
			var dinfo = new DamageInfo(
				DamageDefOf.Blunt,
				lanceDamage,
				armorPenetration: 0.1f,
				instigator: crossFaction ? null : pawn,
				weapon: weapon.def
			);
			JoustingLanceUtility.ApplyJoustHitRegion(ref dinfo);
			Opponent.TakeDamage(dinfo);
		}
	}
}

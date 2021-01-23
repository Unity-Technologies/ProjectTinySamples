using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny;
using Unity.Tiny.Text;
using Unity.Tiny.UI;
using Debug = UnityEngine.Debug;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update start of race countdown label, rank labels and lap labels
    /// </summary>
    public class UpdateGameplayMenu : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!HasSingleton<Race>())
            {
                return;
            }

            var race = GetSingleton<Race>();
            var player = GetSingletonEntity<Player>();

            // Update Countdown label
            var countdownTimer = race.IsInProgress() ? (int) math.ceil(race.CountdownTimer) : 0;

            var playerRank = GetComponent<CarRank>(player);

            var playerLap = GetComponent<LapProgress>(player).CurrentLap;

            Entities.ForEach((Entity e, ref TextRenderer tr, ref RectTransform rect, ref UIName uiName) =>
            {
                if (uiName.Name == "LabelCountdown")
                {
                    if (countdownTimer > 0)
                    {
                        rect.Hidden = false;
                        if (countdownTimer >= 3)
                        {
                            tr.Size = 2000f;
                            tr.MeshColor = Colors.White;
                        }
                        else if (countdownTimer == 2)
                        {
                            tr.Size = 2500f;
                            tr.MeshColor = Colors.Yellow;
                        }
                        else if (countdownTimer == 1)
                        {
                            tr.Size = 3000f;
                            tr.MeshColor = Colors.Red;
                        }

                        TextLayout.SetEntityTextRendererString(EntityManager, e, $"{countdownTimer}");
                    }
                    else
                    {
                        rect.Hidden = true;
                    }
                }

                // Update the lap/rank/etc. labels
                if (uiName.Name == "LabelLap")
                {
                    TextLayout.SetEntityTextRendererString(EntityManager, e, $"LAP {playerLap} / {race.LapCount}");
                }

                if (uiName.Name == "LabelRank")
                {
                    TextLayout.SetEntityTextRendererString(EntityManager, e, $"{playerRank.Value}");
                }

                if (uiName.Name == "LabelRankTotal")
                {
                    // TODO just do this once at the start of a race, the values won't change!
                    TextLayout.SetEntityTextRendererString(EntityManager, e, $"/ {race.NumCars}");
                }

                if (uiName.Name == "LabelTime")
                {
                    // Update the lap time and offset from leader labels
                    var raceTime = race.RaceTimer;
                    var includeMilliseconds =
                        false; // we need to use a monospaced font for this readout, to not make the numebrs shift
                    TextLayout.SetEntityTextRendererString(EntityManager, e,
                        FormatTime(raceTime, includeMilliseconds: includeMilliseconds));
                }

                if (uiName.Name == "LabelTimeFromLeader")
                {
                    if (playerLap > 1 && race.OthersLapTime > 0.0f)
                    {
                        rect.Hidden = false;
                        var timeDiff = race.OthersLapTime - playerRank.LastLapTime;
                        // timediff will be positive if player is in the lead, or negative if not
                        tr.MeshColor = timeDiff > 0.0f ? Colors.Green : Colors.Red;
                        TextLayout.SetEntityTextRendererString(EntityManager, e, FormatTime(timeDiff, true, true));
                    }
                    else
                    {
                        rect.Hidden = true;
                    }
                }
            }).WithStructuralChanges().WithReadOnly(playerLap).WithReadOnly(race).Run();
        }

        private void AppendTime(ref FixedString32 timestr, double time, bool includeMilliseconds = false)
        {
            // time is in seconds
            var min = (int) time / 60;
            var sec = (int) math.floor(time - min * 60.0);

            // gross hacks without string building & formatting
            if (min > 9)
            {
                timestr.Append(min / 10);
            }

            timestr.Append(min % 10);
            timestr.Append(':');

            timestr.Append(sec / 10);
            timestr.Append(sec % 10);

            if (includeMilliseconds)
            {
                //var ms = (int)math.floor((time - (min * 60.0) - sec) * 1000.0f);
                var ms = (int) math.floor((time - min * 60.0 - sec) * 100.0f);
                timestr.Append('.');
                //if (ms < 1000.0)
                //    timestr.Append('0')
                //if (ms < 100.0)
                //    timestr.Append('0')
                if (ms < 10.0)
                {
                    timestr.Append('0');
                }

                timestr.Append(ms);
            }
        }

        private string FormatTime(double time, bool includeSign = false, bool includeMilliseconds = false)
        {
            FixedString32 timestr = default;
            if (time < 0.0f)
            {
                timestr.Append('-');
                time = -time;
            }
            else if (includeSign && math.abs(time) >= 0.00)
            {
                timestr.Append('+');
            }

            AppendTime(ref timestr, time, includeMilliseconds);
            return timestr.ToString();
        }
    }
}

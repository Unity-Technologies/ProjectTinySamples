using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Tiny;
using Unity.Tiny.Audio;
using Unity.Tiny.Text;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update start of race countdown label, rank labels and lap labels
    /// </summary>
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class UpdateGameplayMenu : SystemBase
    {
        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();
            var player = GetSingletonEntity<PlayerTag>();

            // Update gameplay menu visibility
            if (!race.IsRaceStarted || race.IsRaceFinished)
            {
                SetMenuVisibility(false);
                return;
            }

            SetMenuVisibility(true);

            var ui = GetSingleton<GameplayUI>();

            // Update Countdown label
            var countdownTimer = race.IsRaceStarted ? (int) math.ceil(race.CountdownTimer) : 0;
            if (countdownTimer > 0)
            {
                EntityManager.RemoveComponent<Disabled>(ui.CountdownLabel);
                var font = GetComponent<TextRenderer>(ui.CountdownLabel);
                if (countdownTimer >= 3)
                {
                    font.Size = 8.0f;
                    font.MeshColor = Colors.White;
                }
                else if (countdownTimer == 2)
                {
                    font.Size = 9.0f;
                    font.MeshColor = Colors.Yellow;
                }
                else if (countdownTimer == 1)
                {
                    font.Size = 10.0f;
                    font.MeshColor = Colors.Red;
                }

                SetComponent(ui.CountdownLabel, font);

                TextLayout.SetEntityTextRendererString(EntityManager, ui.CountdownLabel, $"{countdownTimer}");
                if (!GetComponent<AudioSource>(ui.CountdownLabel).isPlaying)
                    EntityManager.AddComponent<AudioSourceStart>(ui.CountdownLabel);
            }
            else
            {
                EntityManager.AddComponent<Disabled>(ui.CountdownLabel);
            }

            // Update the lap/rank/etc. labels

            var playerLap = GetComponent<LapProgress>(player).CurrentLap;
            TextLayout.SetEntityTextRendererString(EntityManager, ui.LapLabel, $"LAP {playerLap} / {race.LapCount}");

            var playerRank = GetComponent<CarRank>(player);
            TextLayout.SetEntityTextRendererString(EntityManager, ui.RankLabel, $"{playerRank.Value}");
            // TODO just do this once at the start of a race, the values won't change!
            TextLayout.SetEntityTextRendererString(EntityManager, ui.RankTotalLabel, $"/ {race.NumCars}");

            // Update the lap time and offset from leader labels
            var raceTime = race.RaceTimer;
            var includeMilliseconds = false; // we need to use a monospaced font for this readout, to not make the numebrs shift
            TextLayout.SetEntityTextRendererString(EntityManager, ui.CurrentTimeLabel, FormatTime(raceTime, includeMilliseconds: includeMilliseconds));

            // Find either the leader's time, or the second car's time
            var otherLapTime = 0.0f;
            Entities.ForEach((Entity entity, ref CarRank rank, ref LapProgress progress) =>
            {
                // we only care if a car has completed one lap
                if (progress.CurrentLap < 2)
                    return;

                // then we want either the #2 car's time if we're the lead, or the lead's car if we're not
                if ((playerRank.Value == 1 && rank.Value == 2) || (playerRank.Value != 1 && rank.Value == 1))
                {
                    otherLapTime = rank.LastLapTime;
                }
            }).Run();

            if (playerLap > 1 && otherLapTime > 0.0f)
            {
                EntityManager.RemoveComponent<Disabled>(ui.TimeFromLeaderLabel);
                var timeDiff = otherLapTime - playerRank.LastLapTime;
                // timediff will be positive if player is in the lead, or negative if not
                var font = GetComponent<TextRenderer>(ui.TimeFromLeaderLabel);
                font.MeshColor = timeDiff > 0.0f ? Colors.Green : Colors.Red;
                SetComponent(ui.TimeFromLeaderLabel, font);
                TextLayout.SetEntityTextRendererString(EntityManager, ui.TimeFromLeaderLabel, FormatTime(timeDiff, includeSign: true, includeMilliseconds: true));
            }
            else
            {
                EntityManager.AddComponent<Disabled>(ui.TimeFromLeaderLabel);
            }
        }

        private void AppendTime(ref FixedString32 timestr, double time, bool includeMilliseconds = false)
        {
            // time is in seconds
            var min = (int) time / 60;
            var sec = (int) math.floor(time - (min * 60.0));

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
                var ms = (int)math.floor((time - (min * 60.0) - sec) * 100.0f);
                timestr.Append('.');
                //if (ms < 1000.0)
                //    timestr.Append('0')
                //if (ms < 100.0)
                //    timestr.Append('0')
                if (ms < 10.0)
                    timestr.Append('0');
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


        private void SetMenuVisibility(bool isVisible)
        {
            if (isVisible)
            {
                Entities
                    .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                    .WithAll<GameplayMenuTag, AudioSource, Disabled>().ForEach((Entity entity) =>
                {
                    EntityManager.AddComponent<AudioSourceStart>(entity);
                }).WithStructuralChanges().Run();

                Entities
                    .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
                    .WithAll<GameplayMenuTag, Disabled>().ForEach((Entity entity) =>
                {
                    EntityManager.RemoveComponent<Disabled>(entity);
                }).WithStructuralChanges().Run();
            }
            else
            {
                Entities.WithAll<GameplayMenuTag>().ForEach((Entity entity) =>
                {
                    EntityManager.AddComponent<Disabled>(entity);
                }).WithStructuralChanges().Run();
            }
        }
    }
}

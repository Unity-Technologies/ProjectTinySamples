using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Audio;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update start of race countdown label, rank labels and lap labels
    /// </summary>
    [UpdateAfter(typeof(ResetRace))]
    public class UpdateGameplayMenu : ComponentSystem
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireSingletonForUpdate<Race>();
            InitEntityQueryCache(16);
        }

        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();

            // Update gameplay menu visibility
            SetMenuVisibility(race.IsRaceStarted);

            // Update Countdown label
            var showCountdown = race.IsRaceStarted && race.CountdownTimer > 0f;
            var countdownTimer = race.CountdownTimer;
            Entities.ForEach((ref LabelCountdownTag labelCountdown, ref LabelNumber labelNumber) =>
            {
                labelNumber.IsVisible = showCountdown;
                if (showCountdown)
                {
                    var number = (int) math.ceil(countdownTimer);
                    labelNumber.Number = number;
                }
            });

            Entities.WithAll<LabelCountdownTag, AudioSource>().ForEach((Entity entity, ref AudioSource audioSource) =>
            {
                if (showCountdown && !audioSource.isPlaying)
                {
                    PostUpdateCommands.AddComponent<AudioSourceStart>(entity);
                }
            });

            // Update rank label
            var rank = 0;
            var currentLap = 0;
            Entities.WithNone<AI>().ForEach((ref CarRank carRank, ref LapProgress lapProgress) =>
            {
                rank = carRank.Value;
                currentLap = lapProgress.CurrentLap;
            });
            Entities.WithAll<LabelRankTag>().ForEach((ref LabelNumber labelNumber) =>
            {
                labelNumber.IsVisible = race.IsRaceStarted;
                labelNumber.Number = rank;
            });

            // Update total number of car label (rank total)
            var carCount = 0;
            Entities.ForEach((ref Car car) => { carCount++; });
            Entities.WithAll<LabelRankTotalTag>().ForEach((ref LabelNumber labelNumber) =>
            {
                labelNumber.IsVisible = race.IsRaceStarted;
                labelNumber.Number = carCount;
            });

            // Update current lap label
            Entities.WithAll<LabelLapCurrentTag>().ForEach((ref LabelNumber labelNumber) =>
            {
                labelNumber.IsVisible = race.IsRaceStarted;
                labelNumber.Number = math.clamp(currentLap, 1, race.LapCount);
            });

            // Update total lap count label
            Entities.WithAll<LabelLapTotalTag>().ForEach((ref LabelNumber labelNumber) =>
            {
                labelNumber.IsVisible = race.IsRaceStarted;
                labelNumber.Number = race.LapCount;
            });
        }

        private void SetMenuVisibility(bool isVisible)
        {
            if (isVisible)
            {
                Entities.WithAll<GameplayMenuTag, AudioSource, Disabled>().ForEach(entity =>
                {
                    PostUpdateCommands.AddComponent<AudioSourceStart>(entity);
                });

                Entities.WithAll<GameplayMenuTag, Disabled>().ForEach(entity =>
                {
                    PostUpdateCommands.RemoveComponent<Disabled>(entity);
                });

                Entities.WithAll<DynamicGameplayMenuTag, Disabled>().ForEach(entity =>
                {
                    PostUpdateCommands.RemoveComponent<Disabled>(entity);
                });
            }
            else
            {
                Entities.WithAll<GameplayMenuTag>().ForEach(entity =>
                {
                    PostUpdateCommands.AddComponent<Disabled>(entity);
                });
                Entities.WithAll<DynamicGameplayMenuTag>().ForEach(entity =>
                {
                    PostUpdateCommands.AddComponent<Disabled>(entity);
                });
            }
        }
    }
}
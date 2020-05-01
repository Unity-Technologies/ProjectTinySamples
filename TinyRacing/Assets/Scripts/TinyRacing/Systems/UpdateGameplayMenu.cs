using Unity.Entities;
using Unity.Mathematics;
using Unity.Tiny.Audio;
using Unity.Transforms;

namespace TinyRacing.Systems
{
    /// <summary>
    ///     Update start of race countdown label, rank labels and lap labels
    /// </summary>
    [UpdateAfter(typeof(ResetRace))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class UpdateGameplayMenu : SystemBase
    {
        protected override void OnUpdate()
        {
            var race = GetSingleton<Race>();

            // Update gameplay menu visibility
            SetMenuVisibility(race.IsRaceStarted && !race.IsRaceFinished);
            // Update Countdown label
            var showCountdown = race.IsRaceStarted && race.CountdownTimer > 0f;
            var countdownTimer = race.CountdownTimer;
            Entities.ForEach((ref LabelCountdownTag labelCountdown, ref LabelNumber labelNumber) =>
            {
                labelNumber.IsVisible = showCountdown;
                if (showCountdown)
                {
                    var number = (int)math.ceil(countdownTimer);
                    labelNumber.Number = number;
                }
            }).WithStructuralChanges().Run();

            Entities.WithAll<LabelCountdownTag>().ForEach((Entity entity, ref AudioSource audioSource) =>
            {
                if (showCountdown && !audioSource.isPlaying)
                {
                    EntityManager.AddComponent<AudioSourceStart>(entity);
                }
            }).WithStructuralChanges().Run();

            // Update rank label
            var rank = 0;
            var currentLap = 0;
            Entities.WithNone<AI>().ForEach((ref CarRank carRank, ref LapProgress lapProgress) =>
            {
                rank = carRank.Value;
                currentLap = lapProgress.CurrentLap;
            }).Run();
            Entities.WithAll<LabelRankTag>().ForEach((ref LabelNumber labelNumber) =>
            {
                labelNumber.IsVisible = race.IsRaceStarted;
                labelNumber.Number = rank;
            }).Run();

            // Update total number of car label (rank total)
            var carCount = 0;
            Entities.ForEach((ref Car car) => { carCount++; }).WithoutBurst().Run();
            Entities.WithAll<LabelRankTotalTag>().ForEach((ref LabelNumber labelNumber) =>
            {
                labelNumber.IsVisible = race.IsRaceStarted;
                labelNumber.Number = carCount;
            }).Run();

            // Update current lap label
            Entities.WithAll<LabelLapCurrentTag>().ForEach((ref LabelNumber labelNumber) =>
            {
                labelNumber.IsVisible = race.IsRaceStarted;
                labelNumber.Number = math.clamp(currentLap, 1, race.LapCount);
            }).Run();

            // Update total lap count label
            Entities.WithAll<LabelLapTotalTag>().ForEach((ref LabelNumber labelNumber) =>
            {
                labelNumber.IsVisible = race.IsRaceStarted;
                labelNumber.Number = race.LapCount;
            }).Run();
        }

        private void SetMenuVisibility(bool isVisible)
        {
            if (isVisible)
            {
                Entities.WithAll<GameplayMenuTag, AudioSource, Disabled>().ForEach((Entity entity) =>
                {
                    EntityManager.AddComponent<AudioSourceStart>(entity);
                }).WithStructuralChanges().Run();

                Entities.WithAll<GameplayMenuTag, Disabled>().ForEach((Entity entity) =>
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

using System;
using Unity.Entities;
using Unity.Tiny.Input;
using Unity.Tiny.Text;

namespace TinyTime
{
    public class DateTimeSystem : SystemBase
    {
        private Entity DateEntity;
        private Entity TimeEntity;

        protected override void OnCreate()
        {
            RequireSingletonForUpdate<TimeData>();
            RequireSingletonForUpdate<DateText>();
            RequireSingletonForUpdate<TimeText>();
            base.OnCreate();
        }

        protected override void OnStartRunning()
        {
            DateEntity = GetSingletonEntity<DateText>();
            TimeEntity = GetSingletonEntity<TimeText>();
            base.OnStartRunning();
        }

        protected override void OnUpdate()
        {
            var currentTime = DateTime.Now;
            var input = World.GetOrCreateSystem<InputSystem>();
            var timeData = GetSingleton<TimeData>();
            var hours = DateTime.Now.Hour;
            timeData.IsNightTime = hours < 6 || hours > 17;

            if (input.GetKey(KeyCode.Space) || (input.IsTouchSupported() && input.TouchCount() > 0))
                timeData.IsNightTime = !timeData.IsNightTime;

            SetSingleton(timeData);
            TextLayout.SetEntityTextRendererString(EntityManager, TimeEntity, TimeFormat(currentTime));
            TextLayout.SetEntityTextRendererString(EntityManager, DateEntity,
                $"{GetDayOfWeek(currentTime)} {currentTime.Month:00}/{currentTime.Day:00}");
        }

        private string GetDayOfWeek(DateTime dateTime)
        {
            switch (dateTime.DayOfWeek)
            {
                case DayOfWeek.Sunday:
                    return "Sunday";
                case DayOfWeek.Monday:
                    return "Monday";
                case DayOfWeek.Tuesday:
                    return "Tuesday";
                case DayOfWeek.Wednesday:
                    return "Wednesday";
                case DayOfWeek.Thursday:
                    return "Thursday";
                case DayOfWeek.Friday:
                    return "Friday";
                case DayOfWeek.Saturday:
                    return "Saturday";
            }

            return "";
        }

        private string TimeFormat(DateTime dateTime)
        {
            var hours = dateTime.Hour;
            if (hours > 11)
                hours = hours - 12;
            return $"{hours:00}:{dateTime.Minute:00}";
        }
    }
}

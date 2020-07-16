#include <Unity/Runtime.h>

#if defined(UNITY_WINDOWS)
#define WIN32_LEAN_AND_MEAN 1
#include <windows.h>

#define USECS_PER_SEC 1000000LL

static LARGE_INTEGER s_PerformanceCounterFrequency;

static inline void InitializePerformanceCounterFrequency()
{
    if (!s_PerformanceCounterFrequency.QuadPart)
    {
        // From MSDN: On systems that run Windows XP or later, the function will always succeed and will thus never return zero.
        // so I'll just assume we never run on older than XP

        BOOL qpfResult = QueryPerformanceFrequency(&s_PerformanceCounterFrequency);
        //IL2CPP_ASSERT(qpfResult != FALSE);
    }
}

DOTS_EXPORT(int64_t)
Time_GetTicksMicrosecondsMonotonic()
{
    InitializePerformanceCounterFrequency();

    LARGE_INTEGER value;
    QueryPerformanceCounter(&value);
    //return utils::MathUtils::A_Times_B_DividedBy_C(value.QuadPart, MTICKS_PER_SEC, s_PerformanceCounterFrequency.QuadPart);
    return (value.QuadPart * USECS_PER_SEC) / s_PerformanceCounterFrequency.QuadPart;
}

#else

#include <time.h>
#if defined(UNITY_MACOSX) || defined(UNITY_LINUX) || defined(__EMSCRIPTEN__) || defined(UNITY_IOS)
#include <sys/time.h>
#endif

const int64_t USEC_PER_SEC = 1000000;

// From il2cpp
DOTS_EXPORT(int64_t)
Time_GetTicksMicrosecondsMonotonic()
{
    struct timeval tv;
#if defined(CLOCK_MONOTONIC) && !UNITY_MACOSX
    struct timespec tspec;
    static struct timespec tspec_freq = {0};
    static int can_use_clock = 0;
    if (!tspec_freq.tv_nsec)
    {
        can_use_clock = clock_getres(CLOCK_MONOTONIC, &tspec_freq) == 0;
    }
    if (can_use_clock)
    {
        if (clock_gettime(CLOCK_MONOTONIC, &tspec) == 0)
        {
            return ((int64_t)tspec.tv_sec * USEC_PER_SEC + tspec.tv_nsec / 1000);
        }
    }

#endif
    if (gettimeofday(&tv, NULL) == 0)
        return ((int64_t)tv.tv_sec * USEC_PER_SEC + tv.tv_usec);
    return 0;
}

#endif

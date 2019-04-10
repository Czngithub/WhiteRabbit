using System.Diagnostics;

/// <summary>
/// 计时器类，封装计时器部分的相关代码
/// </summary>
namespace WhiteRabbit.Framework
{
    public class GameTimer
    {
        private readonly double secondsPerCount; //性能计时器中每个计数所代表的秒数
        private double deltaTime;       //本帧与前一帧的时间间隔

        private long baseTime;          //应用程序的开始时刻
        private long pausedTime;        //所有暂停时间之和
        private long stopTime;          //计时器暂停的时刻
        private long prevTime;          //本帧与下一帧的时间差
        private long currTime;          //本帧开始显示的时刻

        private bool stopped;

        public GameTimer()
        {
            Debug.Assert(Stopwatch.IsHighResolution,
                "System does not support high-resolution performance counter");

            secondsPerCount = 0.0;
            deltaTime = -1.0;
            baseTime = 0;
            pausedTime = 0;
            prevTime = 0;
            currTime = 0;
            stopped = false;

            long countsPerSec = Stopwatch.Frequency;
            secondsPerCount = 1.0 / countsPerSec;
        }

        //计算总时间
        public float TotalTime
        {
            get
            {
                if (stopped)
                    return (float)(((stopTime - pausedTime) - baseTime) * secondsPerCount);

                return (float)(((currTime - pausedTime) - baseTime) * secondsPerCount);
            }
        }

        public float DeltaTime => (float)deltaTime;


        //在开始消息循环之前调用
        public void Reset()
        {
            long curTime = Stopwatch.GetTimestamp();
            baseTime = curTime;
            prevTime = curTime;
            stopTime = 0;
            stopped = false;
        }

        //解除计时器暂停时调用
        public void Start()
        {
            long startTime = Stopwatch.GetTimestamp();
            if (stopped)
            {
                pausedTime += (startTime - stopTime);
                prevTime = startTime;
                stopTime = 0;
                stopped = false;
            }
        }

        //暂停计时器时调用
        public void Stop()
        {
            if (!stopped)
            {
                long curTime = Stopwatch.GetTimestamp();
                stopTime = curTime;
                stopped = true;
            }
        }

        //每帧都要调用
        public void Tick()
        {
            if (stopped)
            {
                deltaTime = 0.0;
                return;
            }

            long curTime = Stopwatch.GetTimestamp();
            currTime = curTime;
            deltaTime = (currTime - prevTime) * secondsPerCount;

            prevTime = currTime;
            if (deltaTime < 0.0)
                deltaTime = 0.0;
        }
    }
}

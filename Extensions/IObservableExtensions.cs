using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Zipper
{
    internal static class IObservableExtensions
    {
		public static IDisposable SubscribeWithoutOverlap<T>(this IObservable<T> source, Action<T> action)
		{
			var isRunning = false;
			var sampler = new Subject<Unit>();

			var sub = source
				// .ObserveOn(Scheduler.Default)
                // .Select(s => {
                //     Console.WriteLine("Pre Sample: " + s);
                //     return s;
                // })
				.Sample(sampler)
				// .Select(s =>
				// {
				// 	Console.WriteLine("Post Sample: " + s);
				// 	return s;
				// })
				// .ObserveOn(Scheduler.Default)
				.Subscribe(l =>
				{
                    isRunning = true;
					action(l);
					isRunning = false;
					sampler.OnNext(Unit.Default);
				});

			// start sampling when we have a first value
			var triggerSub = source
				// .ObserveOn(Scheduler.Default)
				.SkipWhile(_ => isRunning)
                .Delay(TimeSpan.FromMilliseconds(10))
                .Subscribe(_ => {
					// Console.WriteLine("Trigger: " + _);
                    sampler.OnNext(Unit.Default);
                });

			return new CompositeDisposable(sub, triggerSub, sampler);
		}
    }
}
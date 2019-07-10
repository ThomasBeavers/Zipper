using System;
using System.Collections.Generic;

namespace Zipper
{
	internal static class IEnumerableExtensions
	{
		internal static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
		{
			foreach (T item in source)
			{
				action(item);
			}
		}
	}
}
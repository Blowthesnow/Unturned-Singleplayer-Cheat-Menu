using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UnturnedSingleplayerCheatMenu.Services;

internal static class ParallelTranslationDispatcher
{
	private sealed class WorkQueue<T>
	{
		private readonly IReadOnlyList<T> _items;

		private int _nextIndex = -1;

		internal WorkQueue(IReadOnlyList<T> items)
		{
			_items = items;
		}

		internal bool TryTake(out T item)
		{
			int index = Interlocked.Increment(ref _nextIndex);
			if (index >= _items.Count)
			{
				item = default(T);
				return false;
			}
			item = _items[index];
			return true;
		}
	}

	internal static Task RunAsync<TWorker, TWorkItem>(IReadOnlyList<TWorker> workers, IReadOnlyList<TWorkItem> workItems, Func<TWorker, TWorkItem, CancellationToken, Task> execute, CancellationToken cancellationToken)
	{
		if (workers == null || workers.Count == 0)
		{
			throw new ArgumentException("At least one translation worker is required.", nameof(workers));
		}
		if (workItems == null)
		{
			throw new ArgumentNullException(nameof(workItems));
		}
		if (execute == null)
		{
			throw new ArgumentNullException(nameof(execute));
		}
		if (workItems.Count == 0)
		{
			return Task.CompletedTask;
		}
		WorkQueue<TWorkItem> queue = new WorkQueue<TWorkItem>(workItems);
		return Task.WhenAll(workers.Select(worker => RunWorkerAsync(worker, queue, execute, cancellationToken)));
	}

	private static async Task RunWorkerAsync<TWorker, TWorkItem>(TWorker worker, WorkQueue<TWorkItem> queue, Func<TWorker, TWorkItem, CancellationToken, Task> execute, CancellationToken cancellationToken)
	{
		while (queue.TryTake(out TWorkItem workItem))
		{
			cancellationToken.ThrowIfCancellationRequested();
			await execute(worker, workItem, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Logging;

namespace Timeout.MessageHandlers
{
	/// <summary>
	/// Thread-safe timeout management class.
	/// </summary>
	public class TimeoutManager : IManageTimeouts
	{
		private static readonly ILog _log = LogManager.GetCurrentClassLogger();

		public event EventHandler<TimeoutData> SagaTimedOut;

		public void Init(TimeSpan interval)
		{
			duration = interval;
		}
		void IManageTimeouts.PushTimeout(TimeoutData timeout)
		{
			lock (data)
			{
				if (!data.ContainsKey(timeout.Time))
					data[timeout.Time] = new List<TimeoutData>();

				data[timeout.Time].Add(timeout);

				if (!sagaLookup.ContainsKey(timeout.SagaId))
					sagaLookup[timeout.SagaId] = new List<DateTime>();

				sagaLookup[timeout.SagaId].Add(timeout.Time);
			}
		}

		void IManageTimeouts.PopTimeout()
		{
			var pair = new KeyValuePair<DateTime, List<TimeoutData>>(DateTime.MinValue, null);

			lock (data)
			{
				if (data.Count > 0)
				{
					var next = data.ElementAt(0);
					if (next.Key - DateTime.UtcNow < duration)
					{
						pair = next;
						data.Remove(pair.Key);

						pair.Value.ForEach(td =>
						{
							List<DateTime> times;
							if (!sagaLookup.TryGetValue(td.SagaId, out times))
								return;

							times.Remove(td.Time);
							if (times.Count == 0)
								sagaLookup.Remove(td.SagaId);
						});
					}
				}
			}

			if (pair.Key == DateTime.MinValue)
			{
				Thread.Sleep(duration);
				return;
			}

			if (pair.Key > DateTime.UtcNow)
				Thread.Sleep(pair.Key - DateTime.UtcNow);

			pair.Value.ForEach(OnSagaTimedOut);
		}

		void IManageTimeouts.ClearTimeout(Guid sagaId)
		{
			lock (data)
			{
				List<DateTime> times;
				if (!sagaLookup.TryGetValue(sagaId, out times))
					return;

				sagaLookup.Remove(sagaId);

				foreach (var time in times)
				{
					List<TimeoutData> timeoutDatas = data[time];

					timeoutDatas.RemoveAll(td => td.SagaId == sagaId);

					if (timeoutDatas.Count == 0)
						data.Remove(time);
				}
			}
		}

		private void OnSagaTimedOut(TimeoutData timeoutData)
		{
			_log.DebugFormat("Timeout fired: SagaId={0}, Expires={1}, State={2}, Sender={3}", timeoutData.SagaId, timeoutData.Time.ToString("u"), timeoutData.State, timeoutData.Destination);

			if (SagaTimedOut != null)
				SagaTimedOut(null, timeoutData);
		}

		private readonly SortedDictionary<DateTime, List<TimeoutData>> data = new SortedDictionary<DateTime, List<TimeoutData>>();
		private readonly Dictionary<Guid, List<DateTime>> sagaLookup = new Dictionary<Guid, List<DateTime>>();

		private TimeSpan duration = TimeSpan.FromMilliseconds(100);
	}
}

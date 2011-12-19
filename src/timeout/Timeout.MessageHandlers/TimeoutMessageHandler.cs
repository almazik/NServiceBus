using Common.Logging;
using NServiceBus;
using NServiceBus.Saga;

namespace Timeout.MessageHandlers
{
    public class TimeoutMessageHandler : IMessageHandler<TimeoutMessage>
    {
    	private static readonly ILog _log = LogManager.GetCurrentClassLogger();

        public IPersistTimeouts Persister { get; set; }
        public IManageTimeouts Manager { get; set; }
        public IBus Bus { get; set; }

        public void Handle(TimeoutMessage message)
        {

            if (message.ClearTimeout)
            {
                Manager.ClearTimeout(message.SagaId);
                Persister.Remove(message.SagaId);
            }
            else
            {
                var data = new TimeoutData
                               {
                                   Destination = Bus.CurrentMessageContext.ReturnAddress,
                                   SagaId = message.SagaId,
                                   State = message.State,
                                   Time = message.Expires
                               };

				_log.DebugFormat("Received TimoutMessage: SagaId={0}, Expires={1}, State={2}, Sender={3}", data.SagaId, data.Time.ToString("u"), data.State, data.Destination);
				
				Manager.PushTimeout(data);
                Persister.Add(data);
            }

            Bus.DoNotContinueDispatchingCurrentMessageToHandlers();
        }
    }
}

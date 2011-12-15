using System;
using System.Linq;
using System.Threading;
using Common.Logging;
using NServiceBus;
using NServiceBus.Saga;
using System.Transactions;
namespace Timeout.MessageHandlers
{
    public class Bootstrapper : IWantToRunAtStartup
    {
		private static readonly ILog _log = LogManager.GetCurrentClassLogger();
		
		public IPersistTimeouts Persister { get; set; }
        public IManageTimeouts Manager { get; set; }
        public IBus Bus { get; set; }

        private Thread thread;
        private volatile bool StopRequested;

        public void Run()
        {
            Manager.SagaTimedOut +=
                (o, e) =>
                    {
                        using (var scope = new TransactionScope(TransactionScopeOption.Required))
                        {
                            Bus.Send(e.Destination,
                                     new TimeoutMessage {SagaId = e.SagaId, Expires = e.Time, State = e.State});
                            Persister.Remove(e.SagaId);

							_log.DebugFormat("Timeout message sent: SagaId={0}, Expires={1}, State={2}, Sender={3}", e.SagaId, e.Time, e.State, e.Destination);

                            scope.Complete();
                        }
                    };

            Persister.GetAll().ToList().ForEach(td => 
                Manager.PushTimeout(td));

            thread = new Thread(Poll);
            thread.Start();
        }

        private void Poll()
        {
            while(!StopRequested)
                Manager.PopTimeout();
        }

        public void Stop()
        {
            StopRequested = true;
        }
    }
}

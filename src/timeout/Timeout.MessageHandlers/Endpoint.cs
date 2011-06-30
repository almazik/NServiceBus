﻿using System;
using System.Configuration;
using NServiceBus;
using NServiceBus.ObjectBuilder;
using NServiceBus.Sagas.Impl;
using Configure = NServiceBus.Configure;

namespace Timeout.MessageHandlers
{
    /// <summary>
    /// Configures the timeout host.
    /// </summary>
    public class Endpoint : IConfigureThisEndpoint, AsA_Server, IWantCustomInitialization, ISpecifyMessageHandlerOrdering
    {
        void IWantCustomInitialization.Init()
        {
            var configure = NServiceBus.Configure.With().Autofac2Builder();

            string nameSpace = ConfigurationManager.AppSettings["NameSpace"];
            string serialization = ConfigurationManager.AppSettings["Serialization"];

            switch (serialization)
            {
                case "xml":
                    configure.XmlSerializer(nameSpace);
                    break;
                case "binary":
                    configure.BinarySerializer();
                    break;
                default:
                    throw new ConfigurationErrorsException("Serialization can only be one of 'interfaces', 'xml', or 'binary'.");
            }

            configure.Configurer.ConfigureComponent<TimeoutManager>(ComponentCallModelEnum.Singleton);
            configure.Configurer.ConfigureComponent<TimeoutPersister>(ComponentCallModelEnum.Singleton)
                .ConfigureProperty(tp => tp.Queue, "timeout.storage");
        }

        void ISpecifyMessageHandlerOrdering.SpecifyOrder(Order order)
        {
            order.Specify(First<TimeoutMessageHandler>.Then<SagaMessageHandler>());
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using System.Runtime.Serialization;
using IServiceOriented.ServiceBus.Threading;
using IServiceOriented.ServiceBus.Collections;
using System.Collections.ObjectModel;
using System.ServiceModel.Channels;
using System.Xml;
using System.ServiceModel.Description;
using System.ServiceModel;
using System.Collections;
using IServiceOriented.ServiceBus.Listeners;


namespace IServiceOriented.ServiceBus.Delivery.Formatters
{
    public abstract class MessageDeliveryConverter
    {
        public const string MaxRetriesHeader = "maxRetries";
        public const string RetryCountHeader = "retryCount";
        public const string MessageIdHeader = "messageId";
        public const string SubscriptionIdHeader = "subscriptionId";
        public const string TimeToProcessHeader = "timeToProcess";
        public const string ContractTypeNameHeader = "contractTypeName";
        public const string ContextHeader = "context";
        public const string MustDeliverByHeader = "mustDeliverBy";
                
        public const string MessagingNamespace = "http://iserviceoriented/servicebus/messaging";
        
        protected abstract Message ToMessageCore(MessageDelivery delivery);
        protected abstract object GetMessageObject(Message message);

        public Message ToMessage(MessageDelivery delivery)
        {
            Type objType = delivery.Message.GetType();

            Message msg = ToMessageCore(delivery);                        
            
            msg.Headers.Add(System.ServiceModel.Channels.MessageHeader.CreateHeader(ContextHeader, MessagingNamespace, delivery.Context)); 
            msg.Headers.Add(System.ServiceModel.Channels.MessageHeader.CreateHeader(MaxRetriesHeader, MessagingNamespace, delivery.MaxRetries));
            msg.Headers.Add(System.ServiceModel.Channels.MessageHeader.CreateHeader(RetryCountHeader, MessagingNamespace, delivery.RetryCount));
            msg.Headers.Add(System.ServiceModel.Channels.MessageHeader.CreateHeader(MessageIdHeader, MessagingNamespace, delivery.MessageDeliveryId));
            msg.Headers.Add(System.ServiceModel.Channels.MessageHeader.CreateHeader(SubscriptionIdHeader, MessagingNamespace, delivery.SubscriptionEndpointId));
            msg.Headers.Add(System.ServiceModel.Channels.MessageHeader.CreateHeader(MustDeliverByHeader, MessagingNamespace, delivery.MustDeliverBy));
            if (delivery.TimeToProcess != null) msg.Headers.Add(System.ServiceModel.Channels.MessageHeader.CreateHeader(TimeToProcessHeader, MessagingNamespace, delivery.TimeToProcess));
            msg.Headers.Add(System.ServiceModel.Channels.MessageHeader.CreateHeader(ContractTypeNameHeader, MessagingNamespace, delivery.ContractTypeName));

            return msg;
        }

        public MessageDelivery ToMessageDelivery(Message msg)
        {                
            MessageDeliveryContext context = msg.Headers.GetHeader<MessageDeliveryContext>(ContextHeader, MessagingNamespace);
            object value = GetMessageObject(msg);            

            int maxRetries = msg.Headers.GetHeader<int>(MaxRetriesHeader, MessagingNamespace);
            int retryCount = msg.Headers.GetHeader<int>(RetryCountHeader, MessagingNamespace);
            string messageId = msg.Headers.GetHeader<string>(MessageIdHeader, MessagingNamespace);
            Guid subscriptionId = msg.Headers.GetHeader<Guid>(SubscriptionIdHeader, MessagingNamespace);
            DateTime? timeToProcess = null;
            if (msg.Headers.FindHeader(TimeToProcessHeader, MessagingNamespace) != -1)
            {
                timeToProcess = msg.Headers.GetHeader<DateTime>(TimeToProcessHeader, MessagingNamespace);
            }
            string contractTypeName = msg.Headers.GetHeader<string>(ContractTypeNameHeader, MessagingNamespace);
            DateTime mustDeliverBy = msg.Headers.GetHeader<DateTime>(MustDeliverByHeader, MessagingNamespace);
            MessageDelivery delivery = new MessageDelivery(messageId, subscriptionId, contractTypeName == null ? null : Type.GetType(contractTypeName), msg.Headers.Action, value, maxRetries, retryCount, timeToProcess, new MessageDeliveryContext(context), mustDeliverBy);
            return delivery;
        }

        public abstract IEnumerable<string> SupportedActions
        {
            get;
        }
        

        public static MessageDeliveryConverter CreateConverter(Type interfaceType)
        {
            ContractDescription description = ContractDescription.GetContract(interfaceType);

            int rawMessageOperationCount = description.Operations.Where( o => o.Messages.First().Body.Parts.First().Type == typeof(Message)).Count();
            int messageContractOperationCount = description.Operations.Where(o => o.Messages.First().MessageType != null).Count();
            
            if (rawMessageOperationCount > 0)
            {
                return new RawMessageMessageDeliveryConverter(interfaceType);
            }
            else if (messageContractOperationCount > 0)
            {
                return new MessageContractMessageDeliveryConverter(interfaceType);
            }
            else
            {
                return new DataContractMessageDeliveryConverter(interfaceType);
            }
        }


        public static MessageDeliveryConverter CreateConverter(params Type[] contractTypes)
        {
            MessageDeliveryConverter[] converters = new MessageDeliveryConverter[contractTypes.Length];

            for(int i = 0; i < contractTypes.Length; i ++)
            {
                converters[i] = CreateConverter(contractTypes[i]);
            }
            return new CompositeMessageDeliveryConverter(converters);
        }
    }

    
}

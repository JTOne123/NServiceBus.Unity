﻿namespace NServiceBus.AcceptanceTests.Sagas
{
    using System;
    using EndpointTemplates;
    using AcceptanceTesting;
    using NServiceBus.Features;

    public class When_doing_request_response_between_sagas : NServiceBusAcceptanceTest
    {
        public class Context : ScenarioContext
        {
            public bool DidRequestingSagaGetTheResponse { get; set; }
            public bool ReplyFromTimeout { get; set; }
            public bool ReplyFromNonInitiatingHandler { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {

            public Endpoint()
            {
                EndpointSetup<DefaultServer>(config => config.EnableFeature<TimeoutManager>());
            }

            public class RequestResponseRequestingSaga : Saga<RequestResponseRequestingSaga.RequestResponseRequestingSagaData>,
                IAmStartedByMessages<InitiateRequestingSaga>,
                IHandleMessages<ResponseFromOtherSaga>
            {
                public Context Context { get; set; }

                public void Handle(InitiateRequestingSaga message)
                {
                    Data.CorrIdForResponse = message.Id;

                    Bus.SendLocal(new RequestToRespondingSaga
                    {
                        SomeIdThatTheResponseSagaCanCorrelateBackToUs = Data.CorrIdForResponse //wont be needed in the future
                    });
                }

                public void Handle(ResponseFromOtherSaga message)
                {
                    Context.DidRequestingSagaGetTheResponse = true;
                    MarkAsComplete();
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<RequestResponseRequestingSagaData> mapper)
                {
                    mapper.ConfigureMapping<InitiateRequestingSaga>(m => m.Id).ToSaga(s => s.CorrIdForResponse);
                    mapper.ConfigureMapping<ResponseFromOtherSaga>(m => m.SomeCorrelationId).ToSaga(s => s.CorrIdForResponse);
                }
                public class RequestResponseRequestingSagaData : ContainSagaData
                {
                    public virtual Guid CorrIdForResponse { get; set; } //wont be needed in the future
                }

            }

            public class RequestResponseRespondingSaga : Saga<RequestResponseRespondingSaga.RequestResponseRespondingSagaData>,
                IAmStartedByMessages<RequestToRespondingSaga>,
                IHandleTimeouts<RequestResponseRespondingSaga.DelayReply>,
                IHandleMessages<SendReplyFromNonInitiatingHandler>
            {
                public Context Context { get; set; }

                public void Handle(RequestToRespondingSaga message)
                {
                    if (Context.ReplyFromNonInitiatingHandler)
                    {
                        Data.CorrIdForRequest = message.SomeIdThatTheResponseSagaCanCorrelateBackToUs; //wont be needed in the future
                        Bus.SendLocal(new SendReplyFromNonInitiatingHandler { SagaIdSoWeCanCorrelate = Data.Id });
                        return;
                    }

                    if (Context.ReplyFromTimeout)
                    {
                        Data.CorrIdForRequest = message.SomeIdThatTheResponseSagaCanCorrelateBackToUs; //wont be needed in the future
                        RequestTimeout<DelayReply>(TimeSpan.FromSeconds(1));
                        return;
                    }

                    Data.CorrIdForRequest = Guid.NewGuid();

                    // Both reply and reply to originator work here since the sender of the incoming message is the requesting saga
                    // also note we don't set the correlation ID since auto correlation happens to work for this special case 
                    // where we reply from the first handler
                    Bus.Reply(new ResponseFromOtherSaga());
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<RequestResponseRespondingSagaData> mapper)
                {
                    mapper.ConfigureMapping<RequestToRespondingSaga>(m => m.SomeIdThatTheResponseSagaCanCorrelateBackToUs).ToSaga(s => s.CorrIdForRequest);
                    //this line is just needed so we can test the non initiating handler case
                    mapper.ConfigureMapping<SendReplyFromNonInitiatingHandler>(m => m.SagaIdSoWeCanCorrelate).ToSaga(s => s.Id);
                }

                public class RequestResponseRespondingSagaData : ContainSagaData
                {
                    public virtual Guid CorrIdForRequest { get; set; }
                }


                public class DelayReply { }

                public void Timeout(DelayReply state)
                {
                    SendReply();
                }

                public void Handle(SendReplyFromNonInitiatingHandler message)
                {
                    SendReply();
                }

                void SendReply()
                {
                    //reply to originator must be used here since the sender of the incoming message the timeoutmanager and not the requesting saga
                    ReplyToOriginator(new ResponseFromOtherSaga //change this line to Bus.Reply(new ResponseFromOtherSaga  and see it fail
                    {
                        SomeCorrelationId = Data.CorrIdForRequest //wont be needed in the future
                    });
                }
            }
        }

        public class InitiateRequestingSaga : ICommand
        {
            public InitiateRequestingSaga()
            {
                Id = Guid.NewGuid();
            }

            public Guid Id { get; set; }
        }

        public class RequestToRespondingSaga : ICommand
        {
            public Guid SomeIdThatTheResponseSagaCanCorrelateBackToUs { get; set; }
        }

        public class ResponseFromOtherSaga : IMessage
        {
            public Guid SomeCorrelationId { get; set; }
        }

        public class SendReplyFromNonInitiatingHandler : ICommand
        {
            public Guid SagaIdSoWeCanCorrelate { get; set; }
        }
    }
}
﻿namespace NServiceBus.AcceptanceTests.Sagas
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_a_existing_saga_instance_exists : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_hydrate_and_invoke_the_existing_instance()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<SagaEndpoint>(b => b.Given(bus =>
                    {
                        bus.SendLocal(new StartSagaMessage { SomeId = Guid.NewGuid() });
                        return Task.FromResult(0);
                    }))
                .Done(c => c.SecondMessageReceived)
                .Run();

            Assert.AreEqual(context.FirstSagaId, context.SecondSagaId, "The same saga instance should be invoked invoked for both messages");
        }

        public class Context : ScenarioContext
        {
            public bool SecondMessageReceived { get; set; }

            public Guid FirstSagaId { get; set; }
            public Guid SecondSagaId { get; set; }
        }

        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class TestSaga05 : Saga<TestSagaData05>, IAmStartedByMessages<StartSagaMessage>
            {
                public Context Context { get; set; }
                public void Handle(StartSagaMessage message)
                {
                    Data.SomeId = message.SomeId;

                    if (message.SecondMessage)
                    {
                        Context.SecondSagaId = Data.Id;
                        Context.SecondMessageReceived = true;
                    }
                    else
                    {
                        Context.FirstSagaId = Data.Id;
                        Bus.SendLocal(new StartSagaMessage { SomeId = message.SomeId, SecondMessage = true });
                    }
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<TestSagaData05> mapper)
                {
                    mapper.ConfigureMapping<StartSagaMessage>(m => m.SomeId)
                        .ToSaga(s => s.SomeId);
                }

            }

            public class TestSagaData05 : IContainSagaData
            {
                public virtual Guid Id { get; set; }
                public virtual string Originator { get; set; }
                public virtual string OriginalMessageId { get; set; }
                public virtual Guid SomeId { get; set; }
            }
        }

        [Serializable]
        public class StartSagaMessage : ICommand
        {
            public Guid SomeId { get; set; }

            public bool SecondMessage { get; set; }
        }
    }
}
using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.NUnit3;
using Khitiara.AkkaUtils;
using NUnit.Framework;

namespace AkkaUtils.Tests
{
    public class Tests : TestKit
    {
        private ActorForwarder<ITestingProxy> _proxy;

        [SetUp]
        public void Setup() {
            _proxy = new ActorForwarderBuilder<ITestingProxy>()
                .Add<TellMsg>(typeof(ITestingProxy).GetMethod("Test1"))
                .Add<AskMsg>(typeof(ITestingProxy).GetMethod("Ask1"))
                .Build();
        }

        [Test]
        public async Task Test1() {
            IActorRef subj = Sys.ActorOf<TestingActor>();
            TestProbe probe = CreateTestProbe();
            subj.Tell(probe, TestActor);
            ExpectMsg("Done", TimeSpan.FromSeconds(1));
            ITestingProxy proxy = _proxy.Create(subj);

            proxy.Test1(1, 2, 0.5);
            probe.ExpectMsg(new TellMsg(1, 2, 0.5));

            Task<AskResp> task = proxy.Ask1(1, 2, "hi");
            probe.ExpectMsg(new AskMsg(1, 2, "hi"));
            AskResp resp = await task;
            Assert.IsTrue(resp.B);
        }
    }
}
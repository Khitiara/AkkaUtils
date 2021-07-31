using System;
using System.Threading.Tasks;
using Akka.Actor;

namespace AkkaUtils.Tests
{
    public record TellMsg(int I, long L, double D);

    public record AskMsg(short S, byte B, string Str);

    public record AskResp(bool B);

    public interface ITestingProxy
    {
        public void Test1(int i, long l, double d);
        public Task<AskResp> Ask1(short s, byte b, string str);
    }

    public class TestingActor : ReceiveActor
    {
        private IActorRef _probe;

        public TestingActor() {
            Receive<IActorRef>(r => {
                _probe = r;
                Sender.Tell("Done");
            });
            Receive<TellMsg>(msg => {
                Console.WriteLine($"Got: {msg}");
                _probe?.Tell(msg);
            });
            Receive<AskMsg>(msg => {
                Console.WriteLine($"Got: {msg}");
                Sender.Tell(new AskResp(true));
                _probe?.Tell(msg);
            });
        }
    }
}
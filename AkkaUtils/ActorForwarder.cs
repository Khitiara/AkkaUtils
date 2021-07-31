using System;
using System.Reflection;
using Akka.Actor;

namespace Khitiara.AkkaUtils
{
    public class ActorForwarder<T>
        where T : class
    {
        private readonly ConstructorInfo _constructor;

        internal ActorForwarder(Type type) {
            _constructor = type.GetConstructor(new[] { typeof(IActorRef) }) ?? throw new InvalidOperationException();
        }

        public T Create(IActorRef actorRef) {
            return _constructor.Invoke(new object?[] { actorRef }) as T ??
                   throw new TargetInvocationException(new InvalidOperationException());
        }
    }
}
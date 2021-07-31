using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;

namespace Khitiara.AkkaUtils
{
    internal static class ActorForwarderBuilder
    {
        internal static readonly Type        CancellationTokenType = typeof(CancellationToken);
        internal static readonly Type        TaskTypeDefinition    = typeof(Task<>);
        private static           MethodInfo? _tell;
        private static readonly  Type[]      TelLArgs = { typeof(IActorRef), typeof(object) };
        private static           MethodInfo? _ask;
        private static readonly  Type[]      AskArgs = { typeof(ICanTell), typeof(object), CancellationTokenType };

        internal static MethodInfo EnsureTell() =>
            LazyInitializer.EnsureInitialized(ref _tell, () =>
                typeof(ActorRefImplicitSenderExtensions).GetMethod("Tell", TelLArgs));

        internal static MethodInfo EnsureAsk() =>
            LazyInitializer.EnsureInitialized(ref _ask, () =>
                typeof(Futures).GetMethod("Ask", 1, AskArgs));
    }

    public class ActorForwarderBuilder<T>
        where T : class
    {
        private readonly Dictionary<MethodInfo, ConstructorInfo> _messageRegistry = new();
        private readonly Type                                    _interfaceType;

        public ActorForwarderBuilder() {
            if (!typeof(T).IsInterface)
                throw new InvalidOperationException();
            _interfaceType = typeof(T);
        }

        public ActorForwarderBuilder<T> Add<TMessage>(MethodInfo targetMethod) {
            Type messageType = typeof(TMessage);
            return Add(targetMethod, messageType);
        }

        public ActorForwarderBuilder<T> Add(MethodInfo targetMethod, Type messageType) {
            Type[] paramTypes = targetMethod.GetParameters().Select(pi => pi.ParameterType).ToArray();
            if (targetMethod.ReturnType.IsGenericType && targetMethod.ReturnType.GetGenericTypeDefinition() ==
                ActorForwarderBuilder.TaskTypeDefinition &&
                paramTypes[^1] == ActorForwarderBuilder.CancellationTokenType) {
                paramTypes = paramTypes[..^1];
            }

            ConstructorInfo? constructorInfo = messageType.GetConstructor(paramTypes);
            _messageRegistry[targetMethod] = constructorInfo ?? throw new MissingMemberException();
            return this;
        }

        public ActorForwarder<T> Build() {
            AssemblyName assemblyName = new(Guid.NewGuid().ToString());
            AssemblyBuilder assemblyBuilder =
                AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            // assemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(
            //     typeof(DebuggableAttribute).GetConstructor(new[] { typeof(DebuggableAttribute.DebuggingModes) }),
            //     new object?[] { DebuggableAttribute.DebuggingModes.DisableOptimizations }));
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name!);
            TypeBuilder typeBuilder =
                moduleBuilder.DefineType($"ActorForwarder_{_interfaceType.Name}", TypeAttributes.Public);
            typeBuilder.AddInterfaceImplementation(_interfaceType);
            FieldBuilder actorRefField = typeBuilder.DefineField("_actor", typeof(IActorRef),
                FieldAttributes.Private | FieldAttributes.InitOnly);

            BuildConstructor(typeBuilder, actorRefField);

            foreach (MethodInfo ifaceMethod in _interfaceType.GetMethods()) {
                BuildMethod(ifaceMethod, typeBuilder, actorRefField);
            }

            return new ActorForwarder<T>(typeBuilder.CreateType() ??
                                         throw new Exception("Failed to create forwarder type"));
        }

        private void BuildMethod(MethodInfo ifaceMethod, TypeBuilder typeBuilder, FieldInfo actorRefField) {
            Type returnType = ifaceMethod.ReturnType;
            if (returnType != typeof(void) && !returnType.IsGenericType)
                if (returnType.GetGenericTypeDefinition() != ActorForwarderBuilder.TaskTypeDefinition)
                    throw new InvalidOperationException();
            if (!_messageRegistry.ContainsKey(ifaceMethod))
                throw new InvalidOperationException();
            ConstructorInfo targetCtor = _messageRegistry[ifaceMethod];
            Type[] paramTypes = ifaceMethod.GetParameters().Select(pi => pi.ParameterType).ToArray();
            bool hasCancel = false;
            if (returnType.IsGenericType &&
                returnType.GetGenericTypeDefinition() == ActorForwarderBuilder.TaskTypeDefinition &&
                paramTypes[^1] == ActorForwarderBuilder.CancellationTokenType) {
                hasCancel = true;
                paramTypes = paramTypes[..^1];
            }

            MethodBuilder methodBuilder = typeBuilder.DefineMethod(ifaceMethod.Name,
                ifaceMethod.Attributes & ~MethodAttributes.Abstract, returnType, paramTypes);
            ILGenerator generator = methodBuilder.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, actorRefField);
            for (int i = 1; i <= paramTypes.Length; i++) {
                generator.EmitLdarg(i);
            }

            generator.Emit(OpCodes.Newobj, targetCtor);
            if (returnType == typeof(void)) {
                generator.EmitCall(OpCodes.Call, ActorForwarderBuilder.EnsureTell(), null);
            } else if (returnType.GetGenericTypeDefinition() == ActorForwarderBuilder.TaskTypeDefinition) {
                Type askRetMessage = returnType.GetGenericArguments()[0];
                if (hasCancel) {
                    generator.Emit(OpCodes.Ldarg, paramTypes.Length + 1);
                } else {
                    // WHY DOES THIS NEED TO BE A LOCAL WHAT
                    generator.DeclareLocal(ActorForwarderBuilder.CancellationTokenType);
                    generator.Emit(OpCodes.Ldloca_S, (byte)0);
                    generator.Emit(OpCodes.Initobj, ActorForwarderBuilder.CancellationTokenType);
                    generator.Emit(OpCodes.Ldloc_0);
                }

                generator.EmitCall(OpCodes.Call, ActorForwarderBuilder.EnsureAsk().MakeGenericMethod(askRetMessage),
                    null);
            }

            generator.Emit(OpCodes.Ret);
            typeBuilder.DefineMethodOverride(methodBuilder, ifaceMethod);
        }

        private static void BuildConstructor(TypeBuilder typeBuilder, FieldInfo actorRefField) {
            ConstructorBuilder ctor = typeBuilder.DefineConstructor(MethodAttributes.Public,
                CallingConventions.Standard, new[] { typeof(IActorRef) });
            ILGenerator generator = ctor.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stfld, actorRefField);
            generator.Emit(OpCodes.Ret);
        }
    }
}
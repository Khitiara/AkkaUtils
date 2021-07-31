using System.Reflection.Emit;

namespace Khitiara.AkkaUtils
{
    public static class Utils
    {
        public static void EmitLdarg(this ILGenerator gen, int arg)
        {
            switch (arg)
            {
                case 0:
                    gen.Emit(OpCodes.Ldarg_0);
                    break;
                case 1:
                    gen.Emit(OpCodes.Ldarg_1);
                    break;
                case 2:
                    gen.Emit(OpCodes.Ldarg_2);
                    break;
                case 3:
                    gen.Emit(OpCodes.Ldarg_3);
                    break;
                default:
                    gen.Emit(OpCodes.Ldarg, arg);
                    break;
            }
        }
    }
}
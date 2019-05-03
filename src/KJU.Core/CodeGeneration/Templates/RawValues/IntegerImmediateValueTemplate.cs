namespace KJU.Core.CodeGeneration.Templates.RawValues
{
    using System;
    using System.Collections.Generic;
    using KJU.Core.Intermediate;

    internal class IntegerImmediateValueTemplate : InstructionTemplate
    {
        public IntegerImmediateValueTemplate()
            : base(new BooleanImmediateValue(false) { TemplateValue = null }, 1)
        {
        }

        public override Instruction Emit(VirtualRegister result, IReadOnlyList<object> fill, string label)
        {
            var value = fill.GetInt(0);
            return new IntegerImmediateValueInstruction(result, value);
        }

        private class IntegerImmediateValueInstruction : Instruction
        {
            private readonly VirtualRegister result;
            private readonly long value;

            public IntegerImmediateValueInstruction(
                VirtualRegister result,
                long value)
                : base(
                    new List<VirtualRegister>(),
                    new List<VirtualRegister> { result },
                    new List<Tuple<VirtualRegister, VirtualRegister>>())
            {
                this.result = result;
                this.value = value;
            }

            public override string ToASM(IReadOnlyDictionary<VirtualRegister, HardwareRegister> registerAssignment)
            {
                var writeTo = this.result.ToHardware(registerAssignment);
                return $"mov {writeTo}, {this.value}";
            }
        }
    }
}
#pragma warning disable SA1008  // Opening parenthesis must not be preceded by a space.
namespace KJU.Core.CodeGeneration.FunctionToAsmGeneration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CfgLinearizer;
    using InstructionSelector;
    using Intermediate;
    using Intermediate.Function;
    using LivenessAnalysis;
    using RegisterAllocation;

    public class FunctionToAsmGenerator : IFunctionToAsmGenerator
    {
        private const int AllocationTriesBound = 4;

        private readonly ILivenessAnalyzer livenessAnalyzer;
        private readonly IRegisterAllocator registerAllocator;
        private readonly IInstructionSelector instructionSelector;
        private readonly ICfgLinearizer cfgLinearizer;
        private readonly ILabelFactory labelFactory = new LabelFactory(new LabelIdGuidGenerator());

        public FunctionToAsmGenerator(
            ILivenessAnalyzer livenessAnalyzer,
            IRegisterAllocator registerAllocator,
            IInstructionSelector instructionSelector,
            ICfgLinearizer cfgLinearizer)
        {
            this.livenessAnalyzer = livenessAnalyzer;
            this.registerAllocator = registerAllocator;
            this.instructionSelector = instructionSelector;
            this.cfgLinearizer = cfgLinearizer;
        }

        public IEnumerable<string> ToAsm(Function function, ILabel cfg)
        {
            var (allocation, instructionSequence) = this.Allocate(function, this.InstructionSequence(cfg));
            return ConstructResult(instructionSequence, allocation, function);
        }

        private static IEnumerable<string> ConstructResult(
            IEnumerable<CodeBlock> instructionSequence,
            RegisterAllocationResult allocation,
            Function function)
        {
            return instructionSequence.SelectMany(codeBlock =>
            {
                return codeBlock.Instructions.SelectMany(instruction => instruction.ToASM(allocation.Allocation))
                    .Prepend($"{codeBlock.Label.Id}:");
            }).Prepend($"{function.MangledName}:");
        }

        private (RegisterAllocationResult, IReadOnlyList<CodeBlock>) Allocate(
            Function function, IReadOnlyList<CodeBlock> instructionSequence)
        {
            for (var iteration = 0; iteration < AllocationTriesBound; ++iteration)
            {
                var interferenceCopyGraphPair = this.livenessAnalyzer.GetInterferenceCopyGraphs(instructionSequence);

                var allowedHardwareRegisters = HardwareRegisterUtils.RegistersForColoring;

                var allocationResult =
                    this.registerAllocator.Allocate(interferenceCopyGraphPair, allowedHardwareRegisters);
                var spilled = new HashSet<VirtualRegister>(allocationResult.Spilled);

                if (spilled.Count == 0)
                {
                    return (allocationResult, instructionSequence);
                }

                instructionSequence = this.PushSpilledOnStack(spilled, instructionSequence, function);
            }

            throw new FunctionToAsmGeneratorException(
                $"Cannot allocate registers without spills after {AllocationTriesBound} times");
        }

        private IReadOnlyList<CodeBlock> PushSpilledOnStack(
            ICollection<VirtualRegister> spilled,
            IEnumerable<CodeBlock> instructionSequence,
            Function function)
        {
            var spilledRegisterToIndexMapping = spilled
                .Select((register, index) => new { Register = register, Index = index })
                .ToDictionary(x => x.Register, x => x.Index);

            var result = instructionSequence.Select(codeBlock =>
            {
                var modifiedInstructions = codeBlock.Instructions.SelectMany(instruction =>
                    this.GetModifiedInstruction(
                        instruction,
                        spilled,
                        spilledRegisterToIndexMapping,
                        function)).ToList();

                return new CodeBlock(codeBlock.Label, modifiedInstructions);
            }).ToList();

            function.StackBytes += 8 * spilled.Count;
            return result;
        }

        private IEnumerable<Instruction> GetModifiedInstruction(
            Instruction instruction,
            ICollection<VirtualRegister> spilled,
            IReadOnlyDictionary<VirtualRegister, int> spilledRegisterToIndexMapping,
            Function function)
        {
            var auxiliaryWrites = instruction.Defines
                .Where(spilled.Contains)
                .SelectMany(register =>
                {
                    var id = spilledRegisterToIndexMapping[register] + 1;
                    var registerVariable = new Intermediate.Variable(function, register);
                    var memoryLocation = new MemoryLocation(function, function.StackBytes + (8 * id));
                    var memoryVariable = new Intermediate.Variable(function, memoryLocation);
                    var readOperation = function.GenerateRead(registerVariable);
                    var writeOperation = function.GenerateWrite(memoryVariable, readOperation);
                    var tree = new Tree(writeOperation, new UnconditionalJump(null));
                    return this.instructionSelector.GetInstructions(tree);
                });

            var auxiliaryReads = instruction.Uses
                .Where(spilled.Contains)
                .SelectMany(register =>
                {
                    var id = spilledRegisterToIndexMapping[register] + 1;
                    var registerVariable = new Intermediate.Variable(function, register);
                    var memoryLocation = new MemoryLocation(function, function.StackBytes + (8 * id));
                    var memoryVariable = new Intermediate.Variable(function, memoryLocation);
                    var readOperation = function.GenerateRead(memoryVariable);
                    var writeOperation = function.GenerateWrite(registerVariable, readOperation);
                    var tree = new Tree(writeOperation, new UnconditionalJump(null));
                    return this.instructionSelector.GetInstructions(tree);
                });

            return auxiliaryReads.Append(instruction).Concat(auxiliaryWrites);
        }

        private IReadOnlyList<CodeBlock> InstructionSequence(ILabel cfg)
        {
            var (orderedTrees, labelToIndexMapping) = this.cfgLinearizer.Linearize(cfg);

            var indexToLabelsMapping =
                labelToIndexMapping
                    .GroupBy(kvp => kvp.Value, kvp => kvp.Key)
                    .ToDictionary(x => x.Key, x => new HashSet<ILabel>(x));

            return orderedTrees.SelectMany((tree, index) =>
            {
                var labelsWithNops = indexToLabelsMapping[index]
                    .Select(label =>
                    {
                        var nopInstruction = new NopInstruction();
                        var nopInstructionBlock = new List<Instruction> { nopInstruction } as IReadOnlyList<Instruction>;
                        label.Tree = new Tree(null, new UnconditionalJump(null));
                        return new CodeBlock(label, nopInstructionBlock);
                    });

                var auxiliaryLabel = this.labelFactory.GetLabel(tree);
                var block = this.instructionSelector.GetInstructions(tree).ToList() as IReadOnlyList<Instruction>;

                var labelBlockTuple = new CodeBlock(auxiliaryLabel, block);

                return labelsWithNops.Append(labelBlockTuple);
            }).ToList();
        }
    }
}
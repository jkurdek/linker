// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Mono.Cecil.Cil;
namespace Mono.Linker.Dataflow
{
	public class BasicBlock
	{
		public List<Instruction> Instructions;

		public BasicBlock ()
		{
			Instructions = new List<Instruction> ();
		}
	}


	class ControlFlowGraph
	{
		Dictionary<BasicBlock, HashSet<BasicBlock>> edges;


		public ControlFlowGraph (MethodBody methodBody)
		{

		}

		private Dictionary<BasicBlock, HashSet<BasicBlock>> SplitMethodBody(MethodBody methodBody)
		{
			var basicBlocks = ConstructBasicBlocks (methodBody);
			var cfg = new Dictionary<BasicBlock, HashSet<BasicBlock>>(basicBlocks.Count);


			foreach (var (_, basicBlock) in basicBlocks) {
				cfg.Add(basicBlock, new HashSet<BasicBlock> ());
			}

			foreach (var (_, basicBlock) in basicBlocks) {
				var jumpTargets = basicBlock.Instructions[^1].GetJumpTargets ();
				foreach (var jumpTarget in jumpTargets) {
					cfg[basicBlock].Add (basicBlocks[jumpTarget.Offset]);
				}
			}

			return cfg;

		}

		private static Dictionary<int, BasicBlock> ConstructBasicBlocks(MethodBody methodBody)
		{
			var basicBlocks = new Dictionary<int, BasicBlock> ();
			var currentBlock = new BasicBlock ();
			
			var leaders = methodBody.GetLeaders ();

			foreach(Instruction operation in methodBody.Instructions) {
				if (leaders.Contains (operation.Offset)) {
					currentBlock = new BasicBlock ();
					basicBlocks.Add (operation.Offset, currentBlock);
				}
				currentBlock.Instructions.Add (operation);
			}

			return basicBlocks;
		}
	}
}

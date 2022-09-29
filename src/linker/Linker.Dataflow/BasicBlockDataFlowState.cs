﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using ILLink.Shared.DataFlow;

namespace Mono.Linker.Dataflow
{
	public class BasicBlockDataFlowState<TValue, TValueLattice>
		: IDataFlowState<BasicBlockState<TValue>, BlockStateLattice<TValue, TValueLattice>>
		where TValue : IEquatable<TValue>
		where TValueLattice : ILattice<TValue>
	{
		BasicBlockState<TValue> current;
		public BasicBlockState<TValue> Current {
			get => current;
			set => current = value;
		}

		public Box<BasicBlockState<TValue>>? Exception { get; set; }

		public BlockStateLattice<TValue, TValueLattice> Lattice { get; init; }

		public void SetLocal (LocalKey key, TValue value)
		{
			current.SetLocal (key, value);
			if (Exception != null)
				Exception.Value = Lattice.Meet (Exception.Value, current);
		}

		public TValue GetLocal (LocalKey key) => current.GetLocal (key);

		public TValue Pop () => current.Pop ();

		public TValue Pop (int count) => current.Pop (count);

		public void Push (TValue value) => current.Push (value);
	}
}

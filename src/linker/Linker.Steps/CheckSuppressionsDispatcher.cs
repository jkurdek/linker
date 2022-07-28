// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ILLink.Shared;
using Mono.Cecil;


namespace Mono.Linker.Steps
{
	public class CheckSuppressionsDispatcher : SubStepsDispatcher
	{
		private LinkContext? _context;

		protected LinkContext Context {
			get {
				Debug.Assert (_context != null);
				return _context;
			}
		}

		public CheckSuppressionsDispatcher () : base (new List<ISubStep> { new CheckSuppressionsStep () })
		{
			
		}

		public override void Process (LinkContext context)
		{
			_context = context;
			base.Process (context);

			var redundantSuppressions = Context.Suppressions.GetUnusedSuppressions ();

			// Suppressions targeting warning caused by anything but the linker should not be reported
			redundantSuppressions = redundantSuppressions
				.Where (suppression => ((DiagnosticId) suppression.suppressMessageInfo.Id).GetDiagnosticCategory () == DiagnosticCategory.Trimming);

			foreach (var (provider, suppressMessageInfo) in redundantSuppressions) {
				var warningOrigin = GetSuppresionOrigin (provider, suppressMessageInfo);

				Context.LogWarning (warningOrigin, DiagnosticId.RedundantSuppression, $"IL{suppressMessageInfo.Id:0000}");
			}
		}

		private MessageOrigin GetSuppresionOrigin (ICustomAttributeProvider provider, SuppressMessageInfo suppressMessageInfo)
		{
			// Suppression defined in XML
			if (Context.Suppressions.TryGetXMLLocation (provider, suppressMessageInfo.Id, out var origin)) {
				return origin;
			}

			// Suppression defined on module
			if (provider is ModuleDefinition module) {
				return new MessageOrigin(module.Assembly);
			}

			return new MessageOrigin (provider);
		}
	}
}

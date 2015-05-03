﻿// This file originally obtained from 
// https://raw.githubusercontent.com/code-cracker/code-cracker/08c1a01337964924eeed12be8b14c8ce8ec6b626/src/Common/CodeCracker.Common/Extensions/GeneratedCodeAnalysisExtensions.cs
// It is subject to the Apache License 2.0
// This file has been modified since obtaining it from its original source.

namespace AsyncUsageAnalyzers
{
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    internal static class GeneratedCodeAnalysisExtensions
    {
        /// <summary>
        /// A cache of the result of computing whether a document has an auto-generated header.
        /// </summary>
        /// <remarks>
        /// This allows many analyzers that run on every token in the file to avoid checking
        /// the same state in the document repeatedly.
        /// </remarks>
        private static readonly ConditionalWeakTable<SyntaxTree, StrongBox<bool?>> GeneratedHeaderPresentCheck
            = new ConditionalWeakTable<SyntaxTree, StrongBox<bool?>>();

        /// <summary>
        /// Checks whether the given node or its containing document is auto generated by a tool.
        /// </summary>
        /// <remarks>
        /// <para>This method uses <see cref="IsGeneratedDocument(SyntaxTree, CancellationToken)"/> to determine which
        /// code is considered "generated".</para>
        /// </remarks>
        /// <param name="context">The analysis context for a <see cref="SyntaxNode"/>.</param>
        /// <returns>
        /// <para><see langword="true"/> if the <see cref="SyntaxNode"/> contained in <paramref name="context"/> is
        /// located in generated code; otherwise, <see langword="false"/>.</para>
        /// </returns>
        internal static bool IsGenerated(this SyntaxNodeAnalysisContext context)
        {
            return IsGeneratedDocument(context.Node.SyntaxTree, context.CancellationToken);
        }

        /// <summary>
        /// Checks whether the given document is auto generated by a tool.
        /// </summary>
        /// <remarks>
        /// <para>This method uses <see cref="IsGeneratedDocument(SyntaxTree, CancellationToken)"/> to determine which
        /// code is considered "generated".</para>
        /// </remarks>
        /// <param name="context">The analysis context for a <see cref="SyntaxTree"/>.</param>
        /// <returns>
        /// <para><see langword="true"/> if the <see cref="SyntaxTree"/> contained in <paramref name="context"/> is
        /// located in generated code; otherwise, <see langword="false"/>.</para>
        /// </returns>
        internal static bool IsGeneratedDocument(this SyntaxTreeAnalysisContext context)
        {
            return IsGeneratedDocument(context.Tree, context.CancellationToken);
        }

        /// <summary>
        /// Checks whether the given document is auto generated by a tool
        /// (based on filename or comment header).
        /// </summary>
        /// <remarks>
        /// <para>The exact conditions used to identify generated code are subject to change in future releases. The current algorithm uses the following checks.</para>
        /// <para>Code is considered generated if it meets any of the following conditions.</para>
        /// <list type="bullet">
        /// <item>The code is contained in a file which starts with a comment containing the text
        /// <c>&lt;auto-generated</c>.</item>
        /// <item>The code is contained in a file with a name matching certain patterns (case-insensitive):
        /// <list type="bullet">
        /// <item>service.cs</item>
        /// <item>TemporaryGeneratedFile_*.cs</item>
        /// <item>assemblyinfo.cs</item>
        /// <item>assemblyattributes.cs</item>
        /// <item>*.g.cs</item>
        /// <item>*.g.i.cs</item>
        /// <item>*.designer.cs</item>
        /// <item>*.generated.cs</item>
        /// <item>*.assemblyattributes.cs</item>
        /// </list>
        /// </item>
        /// </list>
        /// </remarks>
        /// <param name="tree">The syntax tree to examine.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
        /// <returns>
        /// <para><see langword="true"/> if <paramref name="tree"/> is located in generated code; otherwise,
        /// <see langword="false"/>. If <paramref name="tree"/> is <see langword="null"/>, this method returns
        /// <see langword="false"/>.</para>
        /// </returns>
        public static bool IsGeneratedDocument(this SyntaxTree tree, CancellationToken cancellationToken)
        {
            if (tree == null)
            {
                return false;
            }

            StrongBox<bool?> cachedResult = GeneratedHeaderPresentCheck.GetOrCreateValue(tree);
            if (cachedResult.Value.HasValue)
            {
                return cachedResult.Value.Value;
            }

            bool autoGenerated = IsGeneratedDocumentNoCache(tree, cancellationToken);

            // Update the strongbox's value with our computed result.
            // This doesn't change the strongbox reference, and its presence in the
            // ConditionalWeakTable is already assured, so we're updating in-place.
            // In the event of a race condition with another thread that set the value,
            // we'll just be re-setting it to the same value.
            cachedResult.Value = autoGenerated;

            return autoGenerated;
        }

        private static bool IsGeneratedDocumentNoCache(SyntaxTree tree, CancellationToken cancellationToken)
        {
            return IsGeneratedFileName(tree.FilePath) || HasAutoGeneratedComment(tree, cancellationToken);
        }

        /// <summary>
        /// Checks whether the given document has an auto-generated comment as its header.
        /// </summary>
        /// <param name="tree">The syntax tree to examine.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> that the task will observe.</param>
        /// <returns>
        /// <para><see langword="true"/> if <paramref name="tree"/> starts with a comment containing the text
        /// <c>&lt;auto-generated</c>; otherwise, <see langword="false"/>.</para>
        /// </returns>
        private static bool HasAutoGeneratedComment(SyntaxTree tree, CancellationToken cancellationToken)
        {
            var root = tree.GetRoot(cancellationToken);

            if (root == null)
            {
                return false;
            }

            var firstToken = root.GetFirstToken();
            SyntaxTriviaList trivia;
            if (firstToken == default(SyntaxToken))
            {
                var token = ((CompilationUnitSyntax)root).EndOfFileToken;
                if (!token.HasLeadingTrivia)
                {
                    return false;
                }

                trivia = token.LeadingTrivia;
            }
            else
            {
                if (!firstToken.HasLeadingTrivia)
                {
                    return false;
                }

                trivia = firstToken.LeadingTrivia;
            }

            var comments = trivia.Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia));
            return comments.Any(t => t.ToString().Contains("<auto-generated"));
        }

        /// <summary>
        /// Checks whether the given document has a filename that indicates it is a generated file.
        /// </summary>
        /// <param name="filePath">The source file name, without any path.</param>
        /// <returns>
        /// <para><see langword="true"/> if <paramref name="filePath"/> is the name of a generated file; otherwise,
        /// <see langword="false"/>.</para>
        /// </returns>
        /// <seealso cref="IsGeneratedDocument(SyntaxTree, CancellationToken)"/>
        private static bool IsGeneratedFileName(string filePath)
        {
            return Regex.IsMatch(
                Path.GetFileName(filePath),
                @"(^service|^TemporaryGeneratedFile_.*|^assemblyinfo|^assemblyattributes|\.(g\.i|g|designer|generated|assemblyattributes))\.(cs|vb)$",
                RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
        }
    }
}

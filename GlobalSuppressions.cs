// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.
//
// NOTE: the CA1822 "mark as static" suppressions that used to live here targeted
// DSharpPlus-era method signatures (which took an InteractionContext parameter).
// Discord.Net's Interactions framework requires slash-command methods to be
// non-static instance methods, so CA1822 no longer fires on them and the
// suppressions were removed as part of the DSharpPlus -> Discord.Net conversion.

using System.Diagnostics.CodeAnalysis;

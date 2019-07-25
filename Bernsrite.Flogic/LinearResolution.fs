﻿namespace Bernsrite.Flogic

/// One step in a linear resolution derivation.
type LinearResolutionDerivationStep =
    {
        /// Clause used in this step.
        CenterClause : Clause

        /// Side clause used in this step.
        SideClause : Clause

        /// Substitution used in this step.
        Substitution : Substitution
    }

/// A linear resolution derivation.
[<StructuredFormatDisplay("{String}")>]
type LinearResolutionDerivation =
    {
        /// Current center clause.
        CenterClause : Clause

        /// Steps leading to current state, in reverse order.
        Steps : List<LinearResolutionDerivationStep>

        /// Database of active clauses.
        Database : Database
    }

    /// Display string.
    member this.ToString(level) =
        seq {
            yield ""
            yield "Steps:" |> Print.indent level
            let steps =
                this.Steps
                    |> List.rev
                    |> Seq.toArray
            for i = 0 to steps.Length - 1 do
                let step = steps.[i]
                yield ""
                yield sprintf "%d. %A" (i + 1) step.CenterClause
                    |> Print.indent (level + 1)
                yield sprintf "with %A" step.SideClause
                    |> Print.indent (level + 3)
                for variableName, term in step.Substitution.Bindings do
                    yield sprintf "%s <- %A" variableName term
                        |> Print.indent (level + 4)

            yield ""
            yield sprintf "Center clause: %A" this.CenterClause
                |> Print.indent level

            yield this.Database.ToString(level + 1)

        } |> String.join "\r\n"

    /// Display string.
    override this.ToString() =
        this.ToString(0)
        
    /// Display string.
    member this.String = this.ToString()

/// Restrictions to avoid runaway search.
type LinearResolutionConfiguration =
    {
        /// Maximum depth of the search tree.
        MaxDepth : int

        /// Maximum # of literals in a clause.
        MaxLiteralCount : int

        /// Maximum # of symbols in a clause.
        MaxSymbolCount : int
    }

module LinearResolutionDerivation =

    /// Creates a derivation for the given clauses.
    let create clauses topClause =
        assert(clauses |> Seq.contains topClause)
        {
            Database = Database.create clauses
            CenterClause = topClause
            Steps = List.empty
        }

    /// Initializes derivations for the given clauses.
    let generate premiseClauses goalClauses =
        let clauses =
            Seq.append premiseClauses goalClauses
                |> Seq.toArray
        [|
                // guess that the last goal clause is most likely to lead to
                // contradiction (e.g. if plausible-assumption then incorrect-conclusion)
            for topClause in goalClauses |> Seq.rev do
                yield create clauses topClause
        |]

/// http://www.cs.miami.edu/home/geoff/Courses/CSC648-12S/Content/LinearResolution.shtml
module LinearResolution =
            
    /// Depth-first search.
    let search config derivation =

        let rec loop depth derivation =
            assert(depth = (derivation.Steps |> Seq.length))
            if depth < config.MaxDepth then

                    // resolve with all possible side clauses
                derivation.Database.Clauses
                    |> Seq.tryPick (fun sideClause ->
                        Clause.resolve derivation.CenterClause sideClause
                            |> Seq.tryPick (fun (resolvent, substitution) ->
                                if resolvent.Literals.Count > config.MaxLiteralCount
                                    || resolvent.SymbolCount > config.MaxSymbolCount then
                                    None
                                elif derivation.Database.Clauses |> Seq.contains resolvent then
                                    None
                                else
                                    let derivation' =
                                        {
                                            derivation with
                                                CenterClause = resolvent
                                                Steps =
                                                    {
                                                        CenterClause = derivation.CenterClause
                                                        SideClause = sideClause
                                                        Substitution = substitution
                                                    } :: derivation.Steps
                                                Database =
                                                    derivation.Database
                                                        |> Database.add resolvent
                                        }
                                    if resolvent.IsEmpty then   // success: empty clause is a contradiction
                                        Some derivation'
                                    else
                                        derivation' |> loop (depth + 1)))
            else None

        derivation |> loop 0

    /// Tries to prove the given goal from the given premises via linear
    /// resolution.
    let tryProve limits premiseClauses goalClauses =
        LinearResolutionDerivation.generate premiseClauses goalClauses
            |> Seq.tryPick (search limits)

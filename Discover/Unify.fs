﻿namespace Discover

/// Substitution of terms for variables.
[<StructuredFormatDisplay("{String}")>]
type Substitution =
    {
        SubstMap : Map<string (*variable name*), Term>
    }

    /// Display string.
    member this.String =
        this.SubstMap
            |> Map.toSeq
            |> Seq.sort
            |> Seq.map (fun (variable, term) ->
                sprintf "%A <- %A" variable term)
            |> String.join ", "

    /// Display string.
    override this.ToString() =
        this.String

module Substitution =

    /// The empty substitution.
    let empty =
        {
            SubstMap = Map.empty
        }

    /// Creates a substitution containing only the given mapping.
    let create (Variable name) term =
        {
            SubstMap = Map [ name, term ]
        }

    /// Applies the given substitution to the given term.
    let rec applyTerm subst = function
        | Term (Variable name) as term ->
            subst.SubstMap
                |> Map.tryFind name
                |> Option.defaultValue term
        | Application (func, terms) ->
            Application (
                func,
                terms
                    |> Array.map (applyTerm subst))

    /// Applies the given substitution to the given literal.
    let applyLiteral subst literal =
        literal |> Literal.map (applyTerm subst)

    /// Answers names of variables in the domain of the given substitution.
    let getDomainVariableNames subst =
        subst.SubstMap
            |> Map.toSeq
            |> Seq.map fst
            |> set

    /// Answers names of variables in the range of the given substitution.
    let getRangeVariableNames subst =
        subst.SubstMap
            |> Map.toSeq
            |> Seq.collect (
                snd
                    >> Term.getVariables
                    >> Seq.map (fun (Variable name) -> name))
            |> set

    /// Indicates whether the given subtitution is pure.
    let isPure subst =
        Set.intersect
            (getDomainVariableNames subst)
            (getRangeVariableNames subst)
            |> Set.isEmpty

    /// Indicates whether the given substitutions are composable.
    let composable subst1 subst2 =
        Set.intersect
            (getDomainVariableNames subst1)
            (getRangeVariableNames subst2)
            |> Set.isEmpty

    /// Creates a new substitution with the same effect as applying
    /// the two given substitutions in order: subst1 >> subst2
    let compose subst1 subst2 =
        assert(isPure subst1)
        assert(isPure subst2)
        assert(composable subst1 subst2)
        {
            SubstMap =
                seq {
                    for (KeyValue(variable1, term1)) in subst1.SubstMap do
                        yield variable1, (term1 |> applyTerm subst2)
                    for (KeyValue(variable2, term2)) in subst2.SubstMap do
                        if subst1.SubstMap.ContainsKey(variable2) |> not then
                            yield variable2, term2
                } |> Map.ofSeq
        }

module Unfiy =

    /// Tries to unify the given terms by adding to the given
    /// substitution.
    let rec private tryUnifyTerms term1 term2 subst =

            // apply substitions found so far
        let term1' = term1 |> Substitution.applyTerm subst
        let term2' = term2 |> Substitution.applyTerm subst

            // if terms match, we've succeeded
        if term1' = term2' then
            Some subst
        else
                // tries to add to the substitution
            let add variable term =

                    // check for variable name conflict
                let occurs =
                    term
                        |> Term.getVariables
                        |> Set.contains variable
                if occurs then None
                else
                        // update the substitution
                    Substitution.compose
                        subst
                        (Substitution.create variable term)
                        |> Some

            match (term1', term2') with

                    // unify term with variable
                | Term variable, term -> add variable term
                | term, Term variable -> add variable term

                    // recurse on subterms
                | Application (function1, terms1),
                  Application (function2, terms2) ->
                    if function1 = function2 then
                        tryUnifyTermArrays terms1 terms2 subst
                    else None

    /// Tries to unify the given arrays of terms.
    and private tryUnifyTermArrays terms1 terms2 subst =
        (subst, Array.zip terms1 terms2)
            ||> Seq.tryFold (fun acc (term1'', term2'') ->
                tryUnifyTerms term1'' term2'' acc)

    /// Tries to unify two literals.
    let tryUnify literal1 literal2 =
        if literal1.IsPositive = literal2.IsPositive
            && literal1.Predicate = literal2.Predicate then
            tryUnifyTermArrays literal1.Terms literal2.Terms Substitution.empty
        else None

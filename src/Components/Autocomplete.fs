﻿namespace Atom.FSharp

open FunScript
open FunScript.TypeScript
open FunScript.TypeScript.fs
open FunScript.TypeScript.child_process
open FunScript.TypeScript.AtomCore
open FunScript.TypeScript.text_buffer

open Atom


[<ReflectedDefinition>]
module AutocompleteProvider =
    [<JSEmitInline("atom.commands.dispatch(atom.views.getView(atom.workspace.getActiveTextEditor()),'autocomplete-plus:activate');")>]
    let dispatchAutocompleteCommand () : unit = failwith "JS"


    let mutable isForced = false
    let mutable lastResult : DTO.CompletionResult option = None
    let mutable emitter : IEmitter option = None
    let mutable lastRow = 0

    type GetSuggestionOptions = {
        editor          : AtomCore.IEditor
        bufferPosition  : TextBuffer.IPoint
        prefix          : string
        scopeDescriptor : string[] }

    type Provider = {
        selector             : string;
        disableForSelector   : string
        inclusionPriority    : int
        excludeLowerPriority : bool
        getSuggestions       : GetSuggestionOptions -> Atom.Promise.Promise  }

    type SuggestionList = { emitter             : IEmitter }
    type Manager =        { suggestionList      : SuggestionList }
    type Module =         { autocompleteManager : Manager }
    type Package =        { mainModule          : Module }

    type Suggestion = {
        text              : string
        replacementPrefix : string
        rightLabel        : string
        ``type``          : string
    }

    let getSuggestion (options:GetSuggestionOptions) =
        if unbox<obj>(options.editor.buffer.file) <> null then
            let path = options.editor.buffer.file.path
            let row = int options.bufferPosition.row + 1
            let col = int options.bufferPosition.column + 1
            let prefix = if options.prefix = "." || options.prefix = "=" then "" else options.prefix
            Atom.Promise.create(fun () ->
                if isForced || lastResult.IsNone || prefix = "" || lastRow <> row  then
                    Events.once Events.Errors (fun result ->
                        Events.once Events.Completion (fun result ->
                            lastRow <- row
                            lastResult <- Some result
                            isForced <- false
                            let r = result.Data
                                    |> Seq.where(fun t -> t.Name.ToLower().Contains(prefix.ToLower()))
                                    |> Seq.map(fun t -> { text =  t.Name
                                                          replacementPrefix = prefix
                                                          rightLabel = t.Glyph
                                                          ``type`` = t.GlyphChar
                                                        } :> obj)
                                    |> Seq.toArray
                            if r.Length > 0 then LanguageService.helptext (r.[0] :?> Suggestion).text
                            r |> Atom.Promise.resolve)
                        LanguageService.completion path row col)
                    LanguageService.parseEditor options.editor
                else
                    isForced <- false
                    let r = lastResult.Value.Data
                            |> Seq.where(fun t ->  t.Name.ToLower().Contains(prefix.ToLower()))
                            |> Seq.map(fun t -> { text =  t.Name
                                                  replacementPrefix = prefix
                                                  rightLabel = t.Glyph
                                                  ``type`` = t.GlyphChar
                                                } :> obj)
                            |> Seq.toArray
                    if r.Length > 0 then LanguageService.helptext (r.[0] :?> Suggestion).text
                    r |> Atom.Promise.resolve)
        else Atom.Promise.create(fun () -> Atom.Promise.resolve [||])


    let private createHelptext () =
        "<div class='type-tooltip tooltip'>
            <div class='tooltip-inner'>TEST</div>
        </div>" |> jq
    let private helptext = createHelptext ()
    let mutable subscription : Disposable option = None


    let private initialize (editor : IEditor) =
        if subscription.IsSome then subscription.Value.dispose ()
        if isFSharpEditor editor then
            subscription <- editor.onDidChangeCursorPosition ((fun _ -> helptext.fadeOut() |> ignore) |> unbox<Function> ) |> Some

    let create () =
        jq(".panes").append helptext |> ignore
        Globals.atom.commands.add("atom-text-editor","fsharp:autocomplete", (fun _ ->
            if emitter.IsNone then
                let package = Globals.atom.packages.getLoadedPackage("autocomplete-plus") |> unbox<Package>
                let e = package.mainModule.autocompleteManager.suggestionList.emitter
                let handler flag =
                    let selected = if flag then (jq "li.selected").prev().find(" span.word-container .word")
                                   else (jq "li.selected").next().find(" span.word-container .word")

                    let text = if selected.length > 0. then
                                    selected.text()
                               else
                                    if flag then
                                        (jq ".suggestion-list-scroller .list-group li").last().find(" span.word-container .word").text()
                                    else
                                        (jq ".suggestion-list-scroller .list-group li").first().find(" span.word-container .word").text()
                    LanguageService.helptext text

                    () :> obj
                e.on("did-select-next", (fun _ -> handler false) |> unbox<Function>) |> ignore
                e.on("did-select-previous", (fun _ -> handler true) |> unbox<Function>) |> ignore
                emitter <- Some e
            dispatchAutocompleteCommand ()
            isForced <- true) |> unbox<Function>) |> ignore

        Events.on Events.Helptext ((fun (n : DTO.HelptextResult) ->
            let li = (jq ".suggestion-list-scroller .list-group li.selected")
            let o = li.offset()
            let list = jq "autocomplete-suggestion-list"
            if JS.isDefined o && li.length > 0. then
                o.left <- o.left + list.width() + 10.
                o.top <- o.top - li.height() - 10.
                helptext.offset(o) |> ignore
                helptext.show() |> ignore
                let n' = jq' helptext.[0].firstElementChild
                n'.empty() |> ignore
                (n.Data.Text |> jq("<div/>").text)
                |> fun n -> n.html()
                |> fun n -> n.Replace("\\n", "</br>")
                |> fun n -> n.Replace("\n" , "</br>")
                |>  n'.append |> ignore

                ) |> unbox<Function>) |> ignore



        Globals.atom.workspace.getActiveTextEditor() |> initialize
        Globals.atom.workspace.onDidChangeActivePaneItem((fun ed -> initialize ed) |> unbox<Function>  ) |> ignore


        { selector = ".source.fsharp"; disableForSelector = ".source.fsharp .string, .source.fsharp .comment"; inclusionPriority = 1; excludeLowerPriority = true; getSuggestions = getSuggestion}

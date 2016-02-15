﻿module Api

open System.Runtime.Serialization
open Suave
open Suave.Operators
open Suave.Filters
open FSharpApiSearch

module Response =

  [<DataContract>]
  type Paging<'T> = {
    [<field: DataMember(Name = "values")>]
    Values: 'T []
  }

module Libraries =

  open Response
 
  [<DataContract>]
  type Library = {
    [<field: DataMember(Name = "name")>]
    Name: string
  }

  let find =
    {
      Values =
        FSharpApiSearchClient.DefaultTargets
        |> List.map (fun x -> { Name = x })
        |> List.toArray
    }

module Api =

  // copy from https://github.com/hafuu/FSharpApiSearch/blob/7acdbcf0b0a7f3331e00d8ebeea816dfab9492ea/src/FSharpApiSearch.Console/Program.fs#L58
  // The MIT License (MIT)
  // Copyright (c) 2015 MIYAZAKI Shohei
  
  let propertyKindText = function
  | PropertyKind.GetSet -> "get set"
  | PropertyKind.Set -> "set"
  | PropertyKind.Get -> "get"

  let apiKindText = function
  | ApiKind.Constructor -> "constructor"
  | ApiKind.ModuleValue -> "module value"
  | ApiKind.StaticMethod -> "static method"
  | ApiKind.StaticProperty prop -> sprintf "static property with %s" (propertyKindText prop)
  | ApiKind.InstanceMethod -> "instance method"
  | ApiKind.InstanceProperty prop -> sprintf "instance property with %s" (propertyKindText prop)
  | ApiKind.Field -> "field"

  // end

  [<DataContract>]
  type Api = {
    [<field: DataMember(Name = "name")>]
    Name: string
    [<field: DataMember(Name = "kind")>]
    Kind: string
    [<field: DataMember(Name = "signature")>]
    Signature: string
  }

  [<DataContract>]
  type SearchResult = {
    [<field: DataMember(Name = "distance")>]
    Distance: int
    [<field: DataMember(Name = "api")>]
    Api: Api
  }

  open Response

  let toSerializable (results: FSharpApiSearch.SearchResult seq) =
    {
      Values =
        results
        |> Seq.map (fun result ->
          {
            Distance = result.Distance
            Api =
              {
                Name = result.Api.Name
                Kind = apiKindText result.Api.Kind
                Signature = Signature.display result.Api.Signature
              }
          })
        |> Seq.toArray
    }

  let client = FSharpApiSearchClient(FSharpApiSearchClient.DefaultTargets, FSharpApiSearchClient.DefaultReferences)

  let search opts (query: string) =
    client.Search(query, opts)
    |> Seq.filter (fun x -> x.Distance < 3)

  module OptionStatus =

    let tryParse = function
    | "enabled" -> Some Enabled
    | "disabled" -> Some Disabled
    | _ -> None

    let parseOrDefault defaultValue value =
      match tryParse value with
      | Some value -> value
      | None -> defaultValue

  module SearchOptions =

    let parse (req: HttpRequest) =
      match (req.queryParam "strict", req.queryParam "similarity") with
      | (Choice1Of2 strict, Choice1Of2 similarity) -> fun opts ->
        {
          StrictQueryVariable = OptionStatus.parseOrDefault Enabled strict
          SimilaritySearching = OptionStatus.parseOrDefault Disabled similarity
        }
      | (Choice1Of2 strict, Choice2Of2 _) -> fun opts ->
        { opts with StrictQueryVariable = OptionStatus.parseOrDefault Enabled strict }
      | (Choice2Of2 _, Choice1Of2 similarity) -> fun opts ->
        { opts with SimilaritySearching = OptionStatus.parseOrDefault Disabled similarity }
      | (Choice2Of2 _, Choice2Of2 _) -> id
      <| SearchOptions.defaultOptions

let app: WebPart =
  choose [
    GET >=> choose [
      path "/api/libraries"
        >=> (Libraries.find |> Json.toJson |> Suave.Successful.ok)
      path "/api/search" >=>
        request (fun req ->
          cond (req.queryParam "query")
            (Api.search (Api.SearchOptions.parse req)
              >> Api.toSerializable
              >> Json.toJson
              >> Suave.Successful.ok)
            never)
    ]
  ]
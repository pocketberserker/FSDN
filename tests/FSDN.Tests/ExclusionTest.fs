﻿module FSDN.Tests.Api.Search.ExclusionTest

open Persimmon
open UseTestNameByReflection
open FSDN.Api.Search

let ``parse query`` = parameterize {
  source [
    ("", [])
    ("mscorlib", ["mscorlib"])
    ("mscorlib+FSharp.Core", ["mscorlib"; "FSharp.Core"])
  ]
  run (fun (value, expected) -> test {
    do! assertEquals (Ok expected) (Exclusion.parse value) 
  })
}

let ``fail to parse`` = parameterize {
  source [
    "+mscorlib"
  ]
  run (fun query -> test {
    do!
      match Exclusion.parse query with
      | Ok x -> fail <| sprintf "expected fail, but was %A" x
      | Result.Error _ -> pass ()
  })
}

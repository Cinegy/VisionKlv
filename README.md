# Cinegy Vision KLV Decoder Library

## Introduction

Use this library to decode Cinegy Vision experimental KLV data from MPEG transport streams. The library takes a dependency on the Cinegy KLV Decoder Library.

## Tools

This KLV format was created during a proof-of-concept project for attaching camera and event metadata more expicitly against a video stream - similar to how the MXF standard allows metadata to be attached to a camera in the field.

The library and tools allow the decoration of pre-generated TS files to have metadata injected against them from a simple JSON input format, or to dump that metadata back from a TS file into a JSON file (you can endlessly cycle the two!)

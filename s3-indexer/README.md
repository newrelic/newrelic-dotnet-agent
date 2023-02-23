# S3 Indexer

Given a bucket and an optional prefix, this will write an `index.html` that contains a directory listing to every "directory" within that bucket.

## Requirements

Docker is required.

## Building

`docker compose up` will build a Golang container and run `make`, which will produce 64-bit binaries for Linux, Windows and macOS under the `bin` folder.

**Note:** This is only required if there are changes to the indexer source code -- the pre-built binaries for the indexer are already included in the repository.

## Configuration

The following environment variables need to be set, using appropriate values:

* `AWS_ACCESS_KEY_ID`
* `AWS_SECRET_ACCESS_KEY`
* `AWS_REGION` (usually `us-east-1`)

## Running

The indexer can run on Linux (`bin/linux/indexer`), Windows (`bin/windows/indexer.exe`) or macOS (`bin/macOS/indexer`)

Command line syntax is

`indexer|indexer.exe -bucket <bucket_name> [-prefix <folder_prefix>] [-upload]`

While `-prefix` is technically optional, you should always specify it to avoid accidentally indexing the entire bucket.

If `-upload` is not specified, indexer will do a "dry run" - it will list the index.html files that would be generated, but won't actually upload any files to the S3 bucket.

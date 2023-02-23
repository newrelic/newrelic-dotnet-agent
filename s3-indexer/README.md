# S3 Indexer

Given a bucket and an optional prefix, this will write an `index.html` that contains a directory listing to every "directory" within that bucket.

## Requirements

Local installation of Docker

## Building

`docker compose up` will build a container and run `make`. When complete, there should be a new `bin/linux/indexer` and `bin/windows/indexer.exe`.

**Note** This is only required if there are changes to the source code -- the pre-built binaries for the indexer are already included in the repository.

## Configuration

The following environment variables need to be set, using appropriate values:

* `AWS_ACCESS_KEY_ID`
* `AWS_SECRET_ACCESS_KEY`
* `AWS_REGION` (usually `us-east-1`)

## Running

The indexer can run either on Linux (`bin/linux/indexer`) or Windows (`bin/windows/indexer.exe`)

Command line syntax is

`indexer|indexer.exe -bucket <bucket_name> [-prefix <folder_prefix>] [-upload]`

`<bucket_name>` is usually either `nr-downloads-private` or `nr-downloads-main`
While `-prefix` is technically optional, you should always specify it to avoid accidentally indexing the entire bucket.

If `-upload` is not specified, indexer will do a "dry run" - it will list the index.html files that would be generated, but won't actually upload any files to the S3 bucket.

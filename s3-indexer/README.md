# S3 Indexer

Given a bucket and an optional prefix, this will write an `index.html` that
contains a directory listing to every "directory" within that bucket.

## Requirements

Works with (at least) Go v1.20

## Building

`docker compose up` will build a container and run `make`. When complete, there should be a new `bin/linux/indexer` and `bin/windows/indexer.exe`.

**Note that this is only required if there are changes to the source code -- the pre-built binaries for the indexer are included in the repository.

## Configuration

The following environment variables need to be set, using appropriate values:

`AWS_ACCESS_KEY_ID`
`AWS_SECRET_ACCESS_KEY`
`AWS_REGION` (usually `us-east-1`)

## Running

The indexer can run either on Linux (`bin/linux/indexer`) or Windows (`bin/windows/indexer.exe`)

Command line syntax is

`indexer -bucket <bucket_name> [-prefix <folder_prefix>] [ -upload ]`

While `-prefix` is technically optional, you should always specify it to avoid indexing more folders than you intend to.

If `-upload` is not specified, indexer will tell you what it would do but not actually upload any files to the S3 bucket.

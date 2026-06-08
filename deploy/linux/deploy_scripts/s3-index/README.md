# S3 Indexer

Given a bucket and an optional prefix, this will write an `index.html` that
contains a directory listing to every "directory" within that bucket.

## Requirements

This is a Go module that uses the [AWS SDK for Go
v2](https://aws.github.io/aws-sdk-go-v2/docs/) and requires Go 1.24 or later.

## Building

`make` should give you a `bin/indexer` that does useful things.

## Not building

You can also `go run` the tool directly. Module dependencies are downloaded on
demand; `make deps` (which runs `go mod download`) can pre-fetch them if you
like.

You can run the tool with:

```sh
go run ./src/indexer
```

## Configuration

The AWS configuration in `~/.aws` will be used, or environment variables can be
set. More details are at the
[AWS SDK for Go v2 documentation](https://aws.github.io/aws-sdk-go-v2/docs/configuring-sdk/).

If you go the environment variable route, you'll need to set `AWS_REGION`
(probably to `us-east-1`), `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`.

To set up an `~/.aws` directory for development and testing, the easiest method
is to install the AWS CLI tool, and then run `aws configure`.

## Cookbook

To generate indexes for the `index` directory in the folder.
bucket:

```sh
./bin/indexer -bucket <folder> -prefix index/
```

Behold! Indexes! (Or, at least, it'll tell you what it would do.)

To *actually* upload the indexes, you have to pass the `-upload` flag.

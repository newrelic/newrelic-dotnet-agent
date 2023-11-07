# S3 Indexer

Given a bucket and an optional prefix, this will write an `index.html` that
contains a directory listing to every "directory" within that bucket.

## Requirements

This requires at least Go 1.5 with `GO15VENDOREXPERIMENT` enabled. In practice,
I would strongly encourage Go 1.6 or later.

## Building

`make` should give you a `bin/indexer` that does useful things.

## Not building

You can also `go run` the tool provided you've already used `go get` to get the
AWS SDK for Go. You can run `make deps` to handle this for you if you like.

Once done, you can run the tool with:

```sh
GOPATH=$(pwd) go run src/indexer/main.go
```

## Configuration

The AWS configuration in `~/.aws` will be used, or environment variables can be
set. More details are at the
[AWS Go documentation](https://docs.aws.amazon.com/sdk-for-go/api/).

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

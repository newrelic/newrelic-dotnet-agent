GO := go
GOPATH := $(shell pwd)
BINARIES := indexer

export GOPATH

all: deps
	go install indexer

clean:
	rm -rf bin pkg

deps:
	go get github.com/aws/aws-sdk-go

test: deps
	go test indexer s3/indexer

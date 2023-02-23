export GOPATH := $(shell pwd)
export GO111MODULE := auto

all: deps linux windows macOS

linux: deps
	GOOS=linux GOARCH=amd64 go build -o bin/linux/indexer indexer

windows: deps
	GOOS=windows GOARCH=amd64 go build -o bin/windows/indexer.exe indexer

macOS:
	GOOS=darwin GOARCH=amd64 go build -o bin/macOS/indexer indexer

clean:
	rm -f bin/linux/indexer bin/windows/indexer.exe bin/macOS/indexer
	rm -rf pkg src/github.com

deps:
	go get github.com/aws/aws-sdk-go

test: deps
	go test indexer s3/indexer

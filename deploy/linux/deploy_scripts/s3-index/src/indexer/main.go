package main

import (
	"bytes"
	"context"
	"flag"
	"html/template"
	"log"

	"github.com/aws/aws-sdk-go-v2/config"
	"github.com/aws/aws-sdk-go-v2/feature/s3/manager"
	"github.com/aws/aws-sdk-go-v2/service/s3"

	"github.com/newrelic/newrelic-dotnet-agent/deploy/linux/deploy_scripts/s3-index/src/s3/indexer"
)

var (
	bucket string
	prefix string
	upload bool
)

const (
	ContentType   = "text/html; charset=UTF-8"
	IndexFileName = "index.html"
)

func indexDirectory(ctx context.Context, t *template.Template, mgr *manager.Uploader, dir *indexer.Directory) error {
	content, err := dir.Index(t)
	if err != nil {
		return err
	}

	target := dir.Prefix + "/" + IndexFileName
	log.Printf("Uploading index with %d dir(s) and %d file(s) for %s/ to %s\n", len(dir.Directories), len(dir.Files), dir.Prefix, target)

	if upload {
		contentType := ContentType
		input := &s3.PutObjectInput{
			Bucket:      &bucket,
			Key:         &target,
			Body:        bytes.NewReader(content),
			ContentType: &contentType,
		}
		if _, err := mgr.Upload(ctx, input); err != nil {
			return err
		}
	}

	return nil
}

func handleDirectory(ctx context.Context, t *template.Template, mgr *manager.Uploader, dir *indexer.Directory) {
	if dir.ShouldIndex(prefix) {
		if err := indexDirectory(ctx, t, mgr, dir); err != nil {
			log.Printf("Error indexing directory %s: %v", dir.Prefix, err)
		}
	}

	for _, d := range dir.Directories {
		handleDirectory(ctx, t, mgr, d)
	}
}

func main() {
	flag.StringVar(&bucket, "bucket", "", "the S3 bucket")
	flag.StringVar(&prefix, "prefix", "", "the prefix to enumerate")
	flag.BoolVar(&upload, "upload", false, "true to actually upload the index files")
	flag.Parse()

	if !upload {
		log.Println("-upload not given; just telling you what would happen instead of actually doing it")
	}

	t := template.New("index")
	t, err := t.Parse(indexer.DefaultTemplate)
	if err != nil {
		log.Fatalln(err)
	}

	ctx := context.Background()

	cfg, err := config.LoadDefaultConfig(ctx)
	if err != nil {
		log.Fatalln(err)
	}

	svc := s3.NewFromConfig(cfg)
	mgr := manager.NewUploader(svc)

	top := indexer.NewTopDirectory()
	if err := top.Enumerate(ctx, svc, bucket, prefix); err != nil {
		log.Fatalln(err)
	}

	handleDirectory(ctx, t, mgr, top)
}

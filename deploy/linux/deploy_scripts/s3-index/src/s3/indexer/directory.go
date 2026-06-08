package indexer

import (
	"bytes"
	"context"
	"html/template"
	"strings"
	"time"

	"github.com/aws/aws-sdk-go-v2/aws"
	"github.com/aws/aws-sdk-go-v2/service/s3"
)

type Directory struct {
	Parent       *Directory
	Prefix       string
	Name         string
	LastModified time.Time
	Directories  map[string]*Directory
	Files        map[string]*File
}

func NewDirectory(prefix, name string, parent *Directory) *Directory {
	return &Directory{
		Parent:       parent,
		Prefix:       prefix,
		Name:         name,
		LastModified: time.Unix(0, 0),
		Directories:  make(map[string]*Directory),
		Files:        make(map[string]*File),
	}
}

func NewTopDirectory() *Directory {
	return NewDirectory("", "", nil)
}

func (d *Directory) Enumerate(ctx context.Context, svc *s3.Client, bucket, prefix string) error {
	params := &s3.ListObjectsV2Input{
		Bucket: aws.String(bucket),
		Prefix: aws.String(prefix),
	}

	paginator := s3.NewListObjectsV2Paginator(svc, params)
	for paginator.HasMorePages() {
		page, err := paginator.NextPage(ctx)
		if err != nil {
			return err
		}

		for _, obj := range page.Contents {
			file := NewFile(obj)

			// Special case: ignore index.html when generating directories.
			if file.Name == "index.html" {
				continue
			}

			// Split up the key and create the directory structure as required within
			// the nested maps.
			parts := strings.Split(*obj.Key, "/")
			node := d
			for i, part := range parts[:len(parts)-1] {
				if node.LastModified.Before(*file.LastModified) {
					node.LastModified = *file.LastModified
				}

				if child, ok := node.Directories[part]; ok {
					node = child
				} else {
					node.Directories[part] = NewDirectory(strings.Join(parts[0:i+1], "/"), part, node)
					node = node.Directories[part]
				}
			}

			if node.LastModified.Before(*file.LastModified) {
				node.LastModified = *file.LastModified
			}

			node.Files[file.Name] = file
		}
	}

	return nil
}

func (d *Directory) Index(t *template.Template) ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := t.Execute(buf, d); err != nil {
		return nil, err
	}

	return buf.Bytes(), nil
}

func (d *Directory) ShouldIndex(prefix string) bool {
	if _, ok := d.Files[".noindex"]; ok {
		return false
	}

	if strings.Index(strings.TrimRight(d.Prefix, "/"), strings.TrimRight(prefix, "/")) != 0 {
		return false
	}

	return true
}

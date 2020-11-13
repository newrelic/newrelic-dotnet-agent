package indexer

import (
	"path"

	"github.com/aws/aws-sdk-go/service/s3"
)

type File struct {
	*s3.Object
	Name string
}

func NewFile(obj *s3.Object) *File {
	return &File{
		Object: obj,
		Name:   path.Base(*obj.Key),
	}
}

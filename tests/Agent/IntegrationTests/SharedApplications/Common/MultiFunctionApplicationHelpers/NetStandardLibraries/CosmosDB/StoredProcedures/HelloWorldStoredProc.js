function hello () {
    var context = getContext();
    var response = context.getResponse();
    response.setBody("Hello, World");
}


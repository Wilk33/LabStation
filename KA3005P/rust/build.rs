fn main()
{
	if std::env::var("CARGO_CFG_TARGET_OS").as_deref() == Ok("windows")
	{
		let mut resource=winresource::WindowsResource::new();
		resource.set_icon("../assets/reference/KoradS.ico");
		resource.compile().expect("Nie udało się osadzić ikony Korada");
	}
}

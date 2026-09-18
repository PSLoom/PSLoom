# The same Fixture profile with all work applied inside the draft; report only.
Invoke-Loom -Draft {
  Thread Fixture

  Style 'prompt:*' 'color' 'Cyan'
  Style 'prompt:*' 'symbol' '❯'
  Style 'prompt:git' 'color' 'Magenta'
  Style 'history:*' 'size' 10000
  Style 'completion:*' 'menu' 'select'
  Style 'completion:git' 'sort' $false

  Treadle glog { git log --oneline --graph --decorate }
  Treadle gst { git status --short }
  Treadle gco { git checkout }
  Treadle ll { Get-ChildItem -Force }

  Box tools {
    Item git
    Item docker
    Item kubectl
  }

  Box later {
    Item one
    Item two
  }

  Set-Alias -Name k -Value kubectl -Scope Global
}

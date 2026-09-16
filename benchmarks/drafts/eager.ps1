# The typical profile with nothing staged. Measure-Startup.ps1 reports it next to typical.ps1, without gating: the difference between
# the two is what Shed buys.
Invoke-Loom -Draft {
  Thread Reed

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

  Sley git -Alias g -Description 'Distributed version control' {
    OptionGroup Common {
      Option --verbose -Alias '-v'
      Option --quiet -Alias '-q'
    }

    Option --version
    Option --help -Alias '-h'

    Command commit -Alias ci -Description 'Record changes' {
      Use OptionGroup Common
      Option --message -Alias '-m' { Argument text }
      Option --amend
      Argument files -Variadic
    }

    Command checkout -Alias co -Description 'Switch branches' {
      Use OptionGroup Common
      Option --branch -Alias '-b' { Argument name }
      Argument target
    }

    Command log -Description 'Show commit logs' {
      Option --oneline
      Option --graph
      Option --max-count -Alias '-n' { Argument count }
    }

    Command remote -Description 'Manage remotes' {
      Command add { Argument name; Argument url }
      Command remove -Alias rm { Argument name }
    }
  }

  Sley docker -Description 'Containers' {
    Command run { Option --detach -Alias '-d'; Option --name { Argument name }; Argument image }
    Command ps { Option --all -Alias '-a' }
  }

  Set-Alias -Name k -Value kubectl -Scope Global
}
